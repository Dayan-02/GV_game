"""Lightweight FBX copies for the isolated Unity review; PBR maps stay external."""
import bpy
from pathlib import Path
root=Path(__file__).resolve().parent
dest=root.parents[2]/'Previews'/'CustomEnemyMotion'/'Assets'/'Models'
for kind in ('Hunter','Runner','Ambusher','Elite'):
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    for data in (bpy.data.meshes,bpy.data.armatures,bpy.data.materials,bpy.data.images):
        for item in list(data):data.remove(item,do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(root/'Exports'/(kind+'.fbx')))
    for mat in bpy.data.materials:
        for node in list(mat.node_tree.nodes):
            if node.type=='TEX_IMAGE':mat.node_tree.nodes.remove(node)
    for im in list(bpy.data.images):bpy.data.images.remove(im)
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH':obj.data.validate();obj.data.update()
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(dest/(kind+'.fbx')),use_selection=True,
        add_leaf_bones=False,bake_anim=False,object_types={'ARMATURE','MESH'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',path_mode='STRIP',embed_textures=False)
    print('PREVIEW_MESH',kind,flush=True)
