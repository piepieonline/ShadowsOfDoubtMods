import os
import sys
import json

import UnityPy

print("Starting Bundle '" + sys.argv[1] + "'")

# Our actual bundle
custom_asset_bundle_path = os.getcwd() + '/Assets/AssetBundles~/' + sys.argv[1]
custom_asset_bundle = UnityPy.load(custom_asset_bundle_path)

# Our custom vanillacontent bundle (Copies the vanillacontent, but won't be used for users)
vanilla_content_bundle = UnityPy.load(os.getcwd() + '/Assets/AssetBundles~/vanillacontent')

# Load the vanilla asset so we can copy the reference (TODO: Can I work out how this format works and write it manually?)
valid_game_bundle_with_ref = UnityPy.load("E:\\SteamLibrary\\steamapps\\common\\Shadows of Doubt\\Shadows of Doubt_Data\\level1")

# Replace the assembly reference

# Replace the vanilla game references

# Map the PathID in vanillacontent to the GUID
old_path_id_to_guid = {}

for obj in vanilla_content_bundle.objects:
    key = obj.path_id
    if obj.container != None:
        guid = None
        with open(os.getcwd() + '/' + obj.container + '.meta', 'r', encoding="utf-8") as file:
            guid = file.readlines()[1][6:-1]

        if key != None and key in old_path_id_to_guid:
            raise RuntimeError('Duplicate key found. This shouldn\'t happen!') 
        old_path_id_to_guid[key] = { 'path_id': obj.path_id, 'path': obj.container, 'guid': guid }

guid_to_new_path_id = {}

error_warning_log = ''

# Map the GUID to the PathID from the actual game
# TODO: Relative path, maybe move this file in setup
path_id_map_dir = "D:\\Game Modding\\ShadowsOfDoubt\\ShadowsOfDoubtEditor\\AuxiliaryFiles\\"
path_id_map_path = path_id_map_dir + "path_id_map_mapped.json" if os.path.exists(path_id_map_dir + "path_id_map_mapped.json") else path_id_map_dir + "path_id_map.json"
with open(path_id_map_path) as guid_to_new_path_id_text:
    for file in json.loads(guid_to_new_path_id_text.read())['Files']:
        for asset in file['Assets']:
            guid_to_new_path_id[asset['GUID']] = asset['PathID']


def replace_item(obj, key):
    global error_warning_log
    for k, v in obj.items():
        if isinstance(v, dict):
            obj[k] = replace_item(v, key)
        if isinstance(obj[k], list):
            for i in range(0, len(obj[k])):
                if isinstance(obj[k][i], dict):
                    obj[k][i] = replace_item(obj[k][i], key)
    if key in obj:
        if obj[key] != 0:
            # print('old id:' + str(obj[key]))
            try:
                guid = old_path_id_to_guid[obj[key]]['guid']
                # print('old id to guid: ' + guid)

                try:
                    new_path_id = guid_to_new_path_id[guid]
                    # print('guid to new id: ' + str(new_path_id))
                    # print('replaced ' + str(obj[key]) + ' with ' + str(new_path_id))
                    
                    obj[key] = new_path_id
                except:
                    error_warning_log += "GUID '" + guid + "' doesn't have a new PathID\r\n"
                    pass
            except:
                error_warning_log += "Old PathID '" + str(obj[key]) + "' doesn't have a GUID\r\n"
                pass
    return obj

for obj in custom_asset_bundle.objects:
    if obj.serialized_type.nodes:
        tree = obj.read_typetree()
        # PathID references are wrong, pointing to our custom vanillacontent bundle. Rewrite to point to the original resources bundle.
        replace_item(tree, 'm_PathID')

        if obj.type.name in ["MonoScript"]:
            if tree['m_AssemblyName'] == "GameDll.dll":
                tree['m_AssemblyName'] = "Assembly-CSharp.dll"
        
        obj.save_typetree(tree)

print(error_warning_log, file=sys.stderr)

# Set the external bundle reference correctly
custom_asset_bundle.assets[0].externals[0] = (valid_game_bundle_with_ref.assets[0].externals[1])

# Save the modified bundle
with open(custom_asset_bundle_path, "wb") as f:
    f.write(custom_asset_bundle.file.save())

print("Bundle '" + sys.argv[1] + "' saved successfully")