# Unity Baked Volumetrics
A **work in progress** graphics solution for completely baked volumetric lighting, meant to be very lightweight and inexpensive for VR *(and non-vr)*. 

***More details will be revealed but it's very much a work in progress...***

# Results
![sponza1](GithubContent/sponza1.jpg)

![sponza2](GithubContent/sponza2.png)

![sponza3](GithubContent/sponza3.png)

![industrial1](GithubContent/industrial1.png)

![yakohama1](GithubContent/yakohama1.png)

![yakohama2](GithubContent/yakohama2.png)

![yakohama3](GithubContent/yakohama3.jpg)

![church1](GithubContent/church1.png)

![church2](GithubContent/church2.png)

# Features

Overview: This is basically lightmapping but for volumetric fog. 

- Completely baked volumetric lighting designed to be lightweight and fast at runtime, and it's baked completely offline in-editor.
- Volumetric lighting can be generated from multiple sources. (Light Probes, CPU raytacer, *Experimental:* Voxelized GPU raytracer, *Experimental:* IBL) 
- Density is baked into the 3d texture (RGB: Color A: Density) and different density types can be used for different looks. (Constant, Luminance Based, Height Based, Luminance Height Based).
- Post Adjustments can be applied to tweak and improve the look of the generated volume (Brightness, Contrast, Saturation, Vibrance, Hue Shift, Gamma, Color Filter, Color Multiplication, and seperable Gaussian Blur).
- Multiple versions of the same effect to suit different needs... (A scene based version, and multiple post process versions)

**NOTE: Constructed on the Built-In Rendering Pipeline.**

# More Context

The general concept is the following...

You define a box volume within your scene. You set the voxel density for ths volume *(or you can set a custom resolution)*, and choose to either sample colors from the scene light probes, or a custom raytracer, or both. Each have their advantages and drawbacks, but from there you use that to generate a 3D texture that is saved into the disk. At runtime, all we need to do is sample this 3D texture and raymarch through it against the scene depth buffer to get the desired effect. Very simple.

There are 2 versions of this effect, one being a typical **Post Process**, the other is a **Scene Based solution** solution...

### Post Processing
The post processing based version is a work in progress currently, and is meant to be the proper implementation of this effect. It also allows me to leverage more techniques to make the effect way more efficent and lightweight at runtime. It works currently... but there is alot more I want to do to it.

Currently right now it imitates the Scene Based solution, and it has a minor optimization implemented to where the volumetrics are rendered at half resolution and upsampled. However you can't stack multiple volumes, and this will be solved in later updates through other means.

### Scene Based
The scene based solution is for circumstances where you don't have the abillity to create custom post processing effects. Why the heck would this be the case? A good example of this scenario is VRChat where you can't make custom post processing effects, but you can still make shaders within the scene itself. 

Note that this version requires that there is a camera rendering a camera depth texture. This works automatically for deferred rendering, but for forward rendering the camera depth texture flag must be enabled. If you don't have access to the main camera properties there are a couple of tricks you can do to enable the rendering of the depth texture in forward rendering.

***Camera Depth Texture Trick 1:*** If that cant be done, a quirk of the post processing stack is that you can enable ambient occlusion which automatically sets off the camera depth texture generation flag and therefore allows this effect to work. If your world doesn't need AO then I suggest putting the quality settings at its lowest so the cost of the AO effect is smaller. The intensity value also needs to be greater than 0 otherwise the effect won't be active.

***Camera Depth Texture Trick 2:***  Courtesy of [orels1](https://github.com/orels1), you can make unity enable depth pass without using AO (in case of VRC where you do not have access to adjusting Main Cam properties). All you need is a directional light with shadows enabled hitting some random empty layer - and unity will enable the depth pass for you.

*Will be elaborated on but yes this has been tested and works on Oculus/Meta Quest 2.*

# TODO

**NOTE:** Some of these are just ideas, and might not actually be implemented.

- **SCENE BASED:** Add option for animated noise.
- **POST PROCESSING:** Use interleaved rendering to save on performance.
- **POST PROCESSING:** Add animated noise option.
- **POST PROCESSING:** Use temporal filtering in-conjunction with animated noise to accumulate samples over multiple frames.
- **POST PROCESSING:** Using a froxel solution to intersect multiple volumes in a scene, so we can raymarch only once rather than raymarch for each volume in the scene which would be dumb. This also allows the abillity to have multiple volumes in the scene in an efficent way for the post processing solution.
- **OFFLINE VOLUME GENERATION:** Improve the cpu raytraced volume speed by multithreading.
- **OFFLINE VOLUME GENERATION:** Create a custom pathtraced/raytraced solution that voxelizes the scene within the volume and traces against it rather than relying on scene light probes which can be low quality (Could also be potentially faster than the current CPU only implemntation of the raytracer?).
- **EDITOR:** Previewing Voxels is really really slow at low density values, need to come up with a different way to preview the different voxels.
- **EDITOR:** Add a context menu item in the scene hiearchy to create a volume.
