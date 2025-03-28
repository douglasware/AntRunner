docker run --gpus all -it --ipc=host --ulimit memlock=-1 --ulimit stack=67108864 --rm nvcr.io/nvidia/pytorch:25.03-py3
https://catalog.ngc.nvidia.com/orgs/nvidia/containers/pytorch

docker run --gpus all --ipc=host --ulimit memlock=-1 --ulimit stack=67108864 --rm -v D:\repos\AntRunner\Sandboxes\Net6AndPython\Nvidia:/app my-torch