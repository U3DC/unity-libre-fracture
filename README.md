# Unity Libre Fracture

![Demo picture](https://gitlab.com/dima13230/unity-libre-fracture/-/raw/9504a9995980bb899bb635e8260c844701a7db86/Pictures/DemoPicture.png)

Unity Libre Fracture is a free and open source fracture system developed for Unity Engine.
It uses NVIDIA Blast wrapper for unity as base from [here](https://forum.unity.com/threads/nvidia-blast.472623) and for concave meshes uses VHACD (Volumetric Hierarchical Approximate Convex Decomposition) from [here](https://github.com/Unity-Technologies/VHACD).

[YouTube Video Demonstration](https://www.youtube.com/watch?v=_vSFzkecSak)

[Usage Tutorial](https://www.youtube.com/watch?v=od1mtc1HcUM)

### Unity Libre Fracture is now part of Verein Community. To preserve the accessibility of the repository from the resources where it was already mentioned, I won't transfer it to Verein Community (at least not yet)

# TODO

**[C]** - critical issue, **[NC]** - non-critical issue, **[F]** - feature

- **[C]** Fix physical stability issue, probably related to the centers of masses (can be noticed on round objects like sphere when fractured)
- **[F]** Distribute mass along chunks with taking their size in account
- **[F]** If FractureObject had any joints attached and it was then fractured, make corresponding joints at the chunk(s) to the same connected body
- **[F]** Mesh deformation system
