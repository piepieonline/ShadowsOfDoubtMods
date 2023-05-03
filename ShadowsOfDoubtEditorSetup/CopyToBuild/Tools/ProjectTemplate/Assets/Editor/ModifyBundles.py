import os
import sys

import UnityPy

bundlePath = os.getcwd() + '/Assets/AssetBundles/' + sys.argv[1]

env = UnityPy.load(bundlePath)

for obj in env.objects:
    if obj.type.name in ["MonoScript"]:
        if obj.serialized_type.nodes: 
            tree = obj.read_typetree()

            if tree['m_AssemblyName'] == "GameDll.dll":
                tree['m_AssemblyName'] = "Assembly-CSharp.dll"
                obj.save_typetree(tree)

with open(bundlePath, "wb") as f:
    f.write(env.file.save())

print("Bundle '" + sys.argv[1] + "' modified successfully")