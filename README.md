# Unity Baked Volumetrics
A **work in progress** graphics solution for completely baked volumetric lighting, meant to be very lightweight and inexpensive. More details will be revealed but it's very much a work in progress...

NOTE: Built-In Rendering Pipeline.

The general concept is the following, you define a volume within your scene. You set the voxel density for ths volume, and choose to either sample colors from the scene light probes, or a custom raytracer. This generates a 3D texture that is saved into the disk, and in the shader all we do at runtime is raymarch through this 3d texture against the scene depth buffer to get the desired effect.

There are 2 versions of this effect, one being a "scene" based solution, and the other being a proper post process version.

The scene based solution is for circumstances where you don't have access to or the abillity to create custom post processing effects. Why the heck would this be the case? A good example of this scenario is VRChat where you can't make custom post processings effect, but you can still make shaders within the scene. Note that this shader requires there is a camera rendering a camera depth texture. This works automatically for deffered rendering, but for forward rendering the camera depth texture flag must be enabled, or if that cant be done, another wierd quirk is that this can also be done by enabling ambient occlusion on the post processing stack which sets off this flag and therefore allows this effect to work.

The post processing based version is a work in progress currently, and is meant to be the proper implementation of this effect. It also allows me to leverage more techniques to make the effect way more efficent and lightweight at runtime.

# Results
![sponza1](GithubContent/sponza1.jpg)

![sponza2](GithubContent/sponza2.png)

![sponza3](GithubContent/sponza3.png)

![yakohama1](GithubContent/yakohama1.png)

![yakohama2](GithubContent/yakohama2.png)

![yakohama3](GithubContent/yakohama3.jpg)

# TODO
1. Finish post processing version of this effect, adding interleaved rendering and temporal filtering to save on performance.
2. For post processing version of this effect, using a froxel to intersect many different volumes.
3. Improve the raytraced volume quality and speed in the offline generation.
