"""Apply final local hand UVs to the saved revision without rebuilding the geometry."""
import bpy,sys,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
from revise_models import hand_material
for kind,width in (("Hunter",1.03),("Runner",.82),("Ambusher",.91),("Elite",1.3)):
    path=ROOT/"Blender"/(kind+"_ReferenceRevision3.blend")
    bpy.ops.wm.open_mainfile(filepath=str(path))
    mesh=bpy.data.objects[kind+"_CustomMesh"]
    hand_material(mesh,kind,width)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    report_path=ROOT/"Evidence"/(kind+".json")
    report=json.loads(report_path.read_text())
    report["output_sha256"]=hashlib.sha256(path.read_bytes()).hexdigest()
    report_path.write_text(json.dumps(report,indent=2))
    bpy.context.scene.render.filepath=str(ROOT/"Previews"/(kind+"_ThreeQuarter.png"))
    bpy.ops.render.render(write_still=True)
    print("HAND_DETAIL_COMPLETE",kind,flush=True)
