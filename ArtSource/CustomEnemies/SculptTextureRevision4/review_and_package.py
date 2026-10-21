"""Read saved Blender assets, record rig/deformation evidence and render a lineup."""
import bpy
import sys
import math
import json
import hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT.parent/"ReferenceRevision3"))
from revise_models import a_pose, set_camera

KINDS=("Hunter","Runner","Ambusher","Elite")
reports=[]
for kind in KINDS:
    path=ROOT/"Blender"/(kind+"_Sculpt4.blend")
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig=bpy.data.objects[kind+"_Rig"];mesh=bpy.data.objects[kind+"_CustomMesh"]
    report=json.loads((ROOT/"Evidence"/(kind+".json")).read_text())
    with bpy.data.libraries.load(report["source"],link=False) as (src,dst):
        dst.armatures=[n for n in src.armatures if n.startswith(kind+"_")]
    old=dst.armatures[0]
    report["rest_pose_max_error"]=max(abs(b.matrix_local[r][c]-old.bones[b.name].matrix_local[r][c])
                                       for b in rig.data.bones for r in range(4) for c in range(4))
    bpy.data.armatures.remove(old)
    report["missing_bone_groups"]=[g.name for g in mesh.vertex_groups if g.name not in rig.data.bones]
    report["atlas_uv_present"]=mesh.data.uv_layers.get("AtlasUV") is not None
    report["packed_images"]=sum(bool(im.packed_file) for im in bpy.data.images)
    report["pose_samples"]=[]
    for mode in ("Walk","Chase","Attack"):
        action=bpy.data.actions[kind+"_"+mode]
        rig.animation_data_create();rig.animation_data.action=action
        rig.animation_data.action_slot=action.slots[0]
        for phase in (.0,.25,.5,.75,1.):
            bpy.context.scene.frame_set(round(action.frame_range[0]+phase*(action.frame_range[1]-action.frame_range[0])))
            evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            data=evaluated.to_mesh()
            coords=[evaluated.matrix_world@v.co for v in data.vertices]
            report["pose_samples"].append(dict(action=mode,phase=phase,
                                               finite=all(all(math.isfinite(c) for c in v) for v in coords),
                                               minimum_z=min(v.z for v in coords)))
            evaluated.to_mesh_clear()
    report["textures"]=[dict(path=p,sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest())
                        for p in report["textures"]]
    reports.append(report)
    print("INSPECTED",kind,"bones",report["bones"],"actions",len(report["actions"]),
          "weights",report["weight_sum_errors"],"rest_error",report["rest_pose_max_error"],flush=True)

bpy.ops.wm.open_mainfile(filepath=str(ROOT/"Blender"/"Hunter_Sculpt4.blend"))
scene=bpy.context.scene
for kind in KINDS[1:]:
    with bpy.data.libraries.load(str(ROOT/"Blender"/(kind+"_Sculpt4.blend")),link=False) as (src,dst):
        dst.objects=[n for n in src.objects if n in (kind+"_Rig",kind+"_CustomMesh")]
        dst.actions=[n for n in src.actions if n.startswith(kind+"_")]
    collection=bpy.data.collections.new(kind+" sculpt revision")
    scene.collection.children.link(collection)
    for obj in dst.objects:collection.objects.link(obj)
for i,kind in enumerate(KINDS):
    rig=bpy.data.objects[kind+"_Rig"];a_pose(rig);rig.location=((i-1.5)*1.25,0,0)
    data=bpy.data.curves.new(kind+" label","FONT")
    data.body=kind.upper();data.align_x="CENTER";data.size=.115
    obj=bpy.data.objects.new(kind+" label",data);scene.collection.objects.link(obj)
    obj.location=(rig.location.x,0,2.28);obj.rotation_euler=(math.pi/2,0,0)
    mat=bpy.data.materials.new(kind+" label ink");mat.diffuse_color=(.025,.03,.035,1)
    data.materials.append(mat)
scene.render.resolution_x=2160;scene.render.resolution_y=1300
scene.cycles.samples=24
set_camera(scene.camera,(0,-10,2.4),(0,0,1.18),5.30)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Blender"/"Four_Enemies_Sculpt4_Review.blend"))
scene.render.filepath=str(ROOT/"Previews"/"Four_Enemies_Actual_Blender.png")
bpy.ops.render.render(write_still=True)
scene.cycles.samples=16
for mode in ("Walk","Attack"):
    for kind in KINDS:
        rig=bpy.data.objects[kind+"_Rig"];action=bpy.data.actions[kind+"_"+mode]
        rig.animation_data_create();rig.animation_data.action=action
        rig.animation_data.action_slot=action.slots[0]
    scene.frame_set(9 if mode=="Walk" else 20)
    scene.render.filepath=str(ROOT/"Previews"/(mode+"_Actual_Blender.png"))
    bpy.ops.render.render(write_still=True)
(ROOT/"Evidence"/"review.json").write_text(json.dumps(reports,indent=2))
print("REVIEW_SAVED",flush=True)
