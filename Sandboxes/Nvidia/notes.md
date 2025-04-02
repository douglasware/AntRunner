docker run --gpus all -it --ipc=host --ulimit memlock=-1 --ulimit stack=67108864 --rm nvcr.io/nvidia/pytorch:25.03-py3
https://catalog.ngc.nvidia.com/orgs/nvidia/containers/pytorch

docker run --gpus all --ipc=host --ulimit memlock=-1 --ulimit stack=67108864 --rm -v D:\repos\AntRunner\Sandboxes\Net6AndPython\Nvidia:/app my-torch

import torch
print(torch.cuda.is_available())

pip install --upgrade-strategy only-if-needed -r requirements.txt

python -c "import numpy as np; import matplotlib.pyplot as plt; x = np.linspace(0, 2 * np.pi, 100); y = np.sin(x); plt.plot(x, y); plt.savefig('sine_wave.png')"

whoami
ls -l /app
apt-get update