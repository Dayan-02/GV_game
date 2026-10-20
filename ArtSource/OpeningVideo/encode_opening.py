"""Encode Unity-rendered city frames plus an original quiet ambient sound bed."""
from pathlib import Path
import subprocess
import sys
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tmp/video-tools'))
import imageio_ffmpeg

source = ROOT / 'ArtSource/OpeningVideo'
destination = ROOT / 'Assets/StreamingAssets/Opening/OutbreakIntro.mp4'
destination.parent.mkdir(parents=True, exist_ok=True)
rate = 44100
t = np.arange(rate * 10) / rate
rng = np.random.default_rng(7)
wind = np.convolve(rng.normal(0, 1, len(t)), np.ones(480)/480, mode='same')
wind /= max(1, np.max(np.abs(wind)) * 4)
bed = .08*np.sin(2*np.pi*49*t) + .035*np.sin(2*np.pi*73.6*t) + .018*np.sin(2*np.pi*117*t)
bed *= .75 + .25*np.sin(2*np.pi*.13*t)
envelope = np.minimum(t/1.5, 1) * np.clip((10-t)/1.2, 0, 1)
left = (bed + wind*.35)*envelope
right = (bed + np.roll(wind, 700)*.35)*envelope
sound = source / 'OriginalAmbient.wav'
with wave.open(str(sound), 'wb') as output:
    output.setnchannels(2)
    output.setsampwidth(2)
    output.setframerate(rate)
    output.writeframes((np.stack([left,right],axis=1)*32767).clip(-32768,32767).astype('<i2').tobytes())

# Two continuous five-second dolly shots. Brief fades hide the camera cut.
# The first shot's fade must not black out the second shot permanently.
filters = ('eq=saturation=0.78:brightness=-0.015:contrast=1.04,vignette=PI/5,'
           'drawbox=x=0:y=0:w=iw:h=48:color=black:t=fill,'
           'drawbox=x=0:y=ih-48:w=iw:h=48:color=black:t=fill,'
           'fade=t=in:st=0:d=0.7,'
           "fade=t=out:st=4.6:d=0.4:enable='lt(t,5)',"
           "fade=t=in:st=5:d=0.4:enable='gte(t,5)',"
           'fade=t=out:st=9.3:d=0.7')
command = [imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-framerate', '24',
           '-i', str(source/'Frames/%04d.png'), '-i', str(sound),
           '-vf', filters, '-c:v', 'libx264', '-preset', 'medium', '-crf', '19',
           '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '128k',
           '-movflags', '+faststart', '-t', '10', str(destination)]
subprocess.run(command, check=True)
print(destination)
