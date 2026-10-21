"""Apply the final close-up review corrections to saved editable materials."""
import bpy
import bmesh
import sys
import json
import hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
import refine_and_bake as r

for kind in ("Runner","Hunter","Ambusher","Elite"):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/"Blender"/(kind+"_Sculpt4_EditableMaterials.blend")))
    bpy.context.preferences.filepaths.save_version=0
    r.RIG=bpy.data.objects[kind+"_Rig"];mesh=bpy.data.objects[kind+"_CustomMesh"]
    r.clear_pose(r.RIG);bpy.context.view_layer.update()
    matrix=r.RIG.matrix_world.inverted()@mesh.matrix_world
    hairverts=set()
    for poly in mesh.data.polygons:
        if "R4_short_matted_hair" in mesh.data.materials[poly.material_index].name:
            hairverts.update(poly.vertices)
    delete=set()
    for group in r.components(mesh.data):
        if len(group)>500 and all(i in hairverts for i in group):delete.update(group)
    if delete:
        bm=bmesh.new();bm.from_mesh(mesh.data);bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm,geom=[bm.verts[i] for i in delete],context="VERTS")
        bm.to_mesh(mesh.data);bm.free()
    r.face_texture(mesh,kind,matrix)
    for mod in mesh.modifiers:
        if mod.type=="ARMATURE":mod.show_render=False;mod.show_viewport=False
    textures=r.bake_atlas(mesh,kind)
    r.a_pose(r.RIG)
    scene=bpy.context.scene;scene.cycles.samples=24
    r.set_camera(scene.camera,(2.4,-6,2.3),(0,0,1.04),2.30 if kind!="Elite" else 2.49)
    scene.render.resolution_x=1000;scene.render.resolution_y=1200
    out=ROOT/"Blender"/(kind+"_Sculpt4.blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(out))
    scene.render.filepath=str(ROOT/"Previews"/(kind+"_Actual_Blender.png"))
    bpy.ops.render.render(write_still=True)
    r.set_camera(scene.camera,(.45,-4,1.83),(0,0,1.70 if kind!="Elite" else 1.82),.65)
    scene.render.resolution_x=1000;scene.render.resolution_y=1000
    scene.render.filepath=str(ROOT/"Previews"/(kind+"_Face_Actual_Blender.png"))
    bpy.ops.render.render(write_still=True)
    path=ROOT/"Evidence"/(kind+".json")
    report=json.loads(path.read_text())
    mesh.data.calc_loop_triangles()
    report.update(vertices=len(mesh.data.vertices),triangles=len(mesh.data.loop_triangles),
                  unweighted_vertices=sum(not v.groups for v in mesh.data.vertices),
                  weight_sum_errors=sum(abs(sum(g.weight for g in v.groups)-1)>.002 for v in mesh.data.vertices),
                  textures=textures,output_sha256=hashlib.sha256(out.read_bytes()).hexdigest(),
                  final_surface_corrections="Removed solid scalp cap; continuous head-side skin treatment; face-to-neck blending.")
    path.write_text(json.dumps(report,indent=2))
    print("FINAL_SURFACE_SAVED",kind,flush=True)
