"""Native Blender inspection, combined editable review, and actual mesh renders."""
import bpy
import sys
import json
import math
import hashlib
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
from revise_models import a_pose,set_camera

KINDS=("Hunter","Runner","Ambusher","Elite")
reports=[]
for kind in KINDS:
    path=ROOT/"Blender"/(kind+"_ReferenceRevision3.blend")
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig=bpy.data.objects[kind+"_Rig"]
    mesh=bpy.data.objects[kind+"_CustomMesh"]
    actions=[a for a in bpy.data.actions if a.name.startswith(kind+"_")]
    report=json.loads((ROOT/"Evidence"/(kind+".json")).read_text())
    report["reopened_successfully"]=True
    report["weight_sum_errors"]=sum(abs(sum(g.weight for g in v.groups)-1)>.002
                                   for v in mesh.data.vertices)
    report["missing_bone_groups"]=[g.name for g in mesh.vertex_groups if g.name not in rig.data.bones]
    report["motion_samples"]=[]
    for mode in ("Walk","Chase","Attack"):
        action=next(a for a in actions if a.name==kind+"_"+mode)
        rig.animation_data_create()
        rig.animation_data.action=action
        rig.animation_data.action_slot=action.slots[0]
        for phase in (0,.25,.5,.75,1):
            bpy.context.scene.frame_set(round(action.frame_range[0]+phase*(action.frame_range[1]-action.frame_range[0])))
            bpy.context.view_layer.update()
            evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            data=evaluated.to_mesh()
            coords=[evaluated.matrix_world@v.co for v in data.vertices]
            finite=all(all(math.isfinite(c) for c in v) for v in coords)
            report["motion_samples"].append(dict(action=mode,phase=phase,
                                                  finite=finite,
                                                  floor_min=min(v.z for v in coords),
                                                  height=max(v.z for v in coords)-min(v.z for v in coords)))
            evaluated.to_mesh_clear()
    report["source_unchanged"]=hashlib.sha256(Path(report["source"]).read_bytes()).hexdigest()==report["source_sha256"]
    if report["unweighted_vertices"] or report["weight_sum_errors"] or report["missing_bone_groups"]:
        raise RuntimeError("Invalid weights: "+kind)
    if not all(s["finite"] for s in report["motion_samples"]):
        raise RuntimeError("Non-finite deformation: "+kind)
    reports.append(report)
    print("VERIFIED",kind,len(actions),"actions",flush=True)

bpy.ops.wm.open_mainfile(filepath=str(ROOT/"Blender"/"Hunter_ReferenceRevision3.blend"))
scene=bpy.context.scene
for kind in KINDS[1:]:
    with bpy.data.libraries.load(str(ROOT/"Blender"/(kind+"_ReferenceRevision3.blend")),link=False) as (src,dst):
        dst.objects=[n for n in src.objects if n in (kind+"_Rig",kind+"_CustomMesh")]
        dst.actions=[n for n in src.actions if n.startswith(kind+"_")]
    col=bpy.data.collections.new(kind+" reference revision")
    scene.collection.children.link(col)
    for ob in dst.objects:col.objects.link(ob)

cam=scene.camera
for i,kind in enumerate(KINDS):
    rig=bpy.data.objects[kind+"_Rig"]
    a_pose(rig);rig.location=((i-1.5)*1.25,0,0)
    data=bpy.data.curves.new(kind+" label","FONT")
    data.body=kind.upper();data.align_x="CENTER";data.size=.12
    ob=bpy.data.objects.new(kind+" label",data);scene.collection.objects.link(ob)
    ob.location=(rig.location.x,0,2.30);ob.rotation_euler=(math.pi/2,0,0)
    mat=bpy.data.materials.new(kind+" label charcoal")
    mat.diffuse_color=(.025,.03,.035,1);ob.data.materials.append(mat)
set_camera(cam,(0,-10,2.4),(0,0,1.18),5.4)
scene.render.resolution_x=1920;scene.render.resolution_y=1080
scene.cycles.samples=16
scene.render.filepath=str(ROOT/"Previews"/"Four_Enemies_Actual_Blender.png")
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Blender"/"Four_Enemies_ReferenceRevision3.blend"))
bpy.ops.render.render(write_still=True)
for mode in ("Walk","Attack"):
    for kind in KINDS:
        rig=bpy.data.objects[kind+"_Rig"]
        action=bpy.data.actions[kind+"_"+mode]
        rig.animation_data_create();rig.animation_data.action=action
        rig.animation_data.action_slot=action.slots[0]
    scene.frame_set(9 if mode=="Walk" else 20)
    scene.render.filepath=str(ROOT/"Previews"/(mode+"_Actual_Blender.png"))
    bpy.ops.render.render(write_still=True)
(ROOT/"Evidence"/"validation.json").write_text(json.dumps(reports,indent=2))
print("REVIEW_COMPLETE",flush=True)
