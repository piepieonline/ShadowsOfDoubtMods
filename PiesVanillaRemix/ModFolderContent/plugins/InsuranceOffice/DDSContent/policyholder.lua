-- Serves policyholder1..N; the trailing digits of the caller pick the slot.
--
-- Every entry replays the same draw sequence from the same seed and stops at its own slot,
-- so the six entries of one ledger are a coherent, duplicate-free set with no shared state
-- to go stale across a city reload.

local ENTRIES        = 6
local DEAD_CHANCE    = 25   -- percent of ledgers carrying one dead insured
local LOCAL_CHANCE   = 70   -- percent of draws preferring the branch's own district
local TIER_TOP       = 60   -- percent drawn from the top salary quintile
local TIER_MID       = 30   -- ...then the middle band; the remainder is open

-- Park-Miller, so determinism is a property of this file rather than of MoonSharp's RNG.
-- Every intermediate stays under 2^53, which keeps the double arithmetic exact.
local function newrng(seed)
    local s = math.floor(seed) % 2147483646
    if s <= 0 then s = s + 2147483646 end
    return function(n)
        s = (s * 16807) % 2147483647
        if n == nil or n <= 0 then return 0 end
        return s % n
    end
end

local function try(f)
    local ok, v = pcall(f)
    if ok then return v end
end

local slot = tonumber(tostring(_caller):match("(%d+)$")) or 1
local o = inputObject

local objectId = try(function() return o.id end)
             or try(function() return o.interactable.id end)
             or 0

local myDistrict = try(function() return o.node.gameLocation.district.districtID end)
                or try(function() return o.worldObjectRoomParent.gameLocation.district.districtID end)

-- Pool: employed citizens with an address, so the residence line always has something.
local pool, seen = {}, {}

local function add(h, salary)
    if h == nil then return end
    local id = tonumber(try(function() return h.humanID end))
    if id == nil or seen[id] then return end
    if try(function() return h.home end) == nil then return end
    seen[id] = true
    pool[#pool + 1] = {
        h    = h,
        id   = id,
        sal  = tonumber(salary) or 0,
        dist = try(function() return h.home.district.districtID end),
        dead = try(function() return h.death.isDead end) == true,
    }
end

local jobs = CityData.assignedJobsDirectory
if jobs ~= nil then
    for i = 0, jobs.Count - 1 do
        local j = jobs[i]
        add(try(function() return j.employee end), try(function() return j.salary end))
    end
end

-- The dead leave the job directory, and they are the whole point of the dead draw.
local gone = CityData.deadCitizensDirectory
if gone ~= nil then
    for i = 0, gone.Count - 1 do
        add(gone[i], try(function() return gone[i].job.salary end))
    end
end

local n = #pool
if n == 0 then return nil end

-- Ascending salary, humanID breaking ties so the band edges are stable across runs.
table.sort(pool, function(a, b)
    if a.sal ~= b.sal then return a.sal < b.sal end
    return a.id < b.id
end)

local topLo = math.floor(n * 0.8) + 1
local midLo = math.floor(n * 0.4) + 1
local midHi = topLo - 1

local dead = {}
for i = 1, n do
    if pool[i].dead then dead[#dead + 1] = i end
end

local function take(rng, list, used)
    local avail = {}
    for _, i in ipairs(list) do
        if not used[i] then avail[#avail + 1] = i end
    end
    if #avail == 0 then return nil end
    return avail[rng(#avail) + 1]
end

local function band(lo, hi, used, district)
    local out = {}
    for i = lo, hi do
        if not used[i] and (district == nil or pool[i].dist == district) then
            out[#out + 1] = i
        end
    end
    return out
end

local rng = newrng(objectId * 7919 + 13)

-- Both ledger-level rolls are drawn unconditionally, so the stream stays aligned for
-- every slot regardless of which branch a given entry ends up taking.
local carriesDead = rng(100) < DEAD_CHANCE
local deadSlot    = rng(ENTRIES) + 1

local used, chosen = {}, nil

for k = 1, slot do
    local pick

    if carriesDead and k == deadSlot and #dead > 0 then
        pick = take(rng, dead, used)
    else
        local tier = rng(100)
        local lo, hi
        if tier < TIER_TOP then
            lo, hi = topLo, n
        elseif tier < TIER_TOP + TIER_MID then
            lo, hi = midLo, midHi
        else
            lo, hi = 1, n
        end
        if lo > hi then lo, hi = 1, n end

        local local_ = myDistrict ~= nil and rng(100) < LOCAL_CHANCE

        local list = band(lo, hi, used, local_ and myDistrict or nil)
        if #list == 0 then list = band(lo, hi, used, nil) end
        if #list == 0 then list = band(1, n, used, nil) end

        if #list > 0 then pick = list[rng(#list) + 1] end
    end

    if pick == nil then return nil end
    used[pick] = true
    chosen = pick
end

return pool[chosen].h
