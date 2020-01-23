**************************************
*          HIGHLIGHT PLUS            *
* Created by Ramiro Oliva (Kronnect) * 
*            README FILE             *
**************************************


Quick help: how to use this asset?
----------------------------------

1) Highlighting specific objects: add HighlightEffect.cs script to any GameObject. Customize the appearance options.
  In the Highlight Effect inspector, you can specify which objects, in addition to this one, are also affected by the effect:
    a) Only this object
    b) This object and its children
    c) All objects from the root to the children
    d) All objects belonging to a layer

2) Control highlight effect when mouse is over:
  Add HighlightTrigger.cs script to the GameObject. It will activate highlight on the gameobject when mouse pass over it.

3) Highlighting any object in the scene:
  Select top menu GameObject -> Effects -> Highlight Plus -> Create Manager.
  Customize appearance and behaviour of Highlight Manager. Those settings are default settings for all objects. If you want different settings for certain objects just add another HighlightEffect script to each different object. The manager will use those settings.

4) Make transparent shaders compatible with See-Through effect:
  If you want the See-Through effect be seen through other transparent objects, they need to be modified so they write to depth buffer (by default transparent objects do not write to z-buffer).
  To do so, select top menu GameObject -> Effects -> Highlight Plus -> Add Depth To Transparent Object.

5) Static batching:
  Objects marked as "static" need a MeshCollider in order to be highlighted. This is because Unity combines the meshes of static objects so it's not possible to highlight individual objects if their meshes are combined.
  To allow highlighting static objects make sure they have a MeshCollider attached (the MeshCollider can be disabled).



Online help & Forum
-------------------

Online manual: https://kronnect.freshdesk.com/support/solutions/42000065090

Have any question or issue?
* Email: contact@kronnect.me
* Support Forum: http://kronnect.me
* Twitter: @KronnectGames

If you like Highlight Plus, please rate it on the Asset Store. It encourages us to keep improving it! Thanks!



Future updates
--------------

All our assets follow an incremental development process by which a few beta releases are published on our support forum (kronnect.com).
We encourage you to signup and engage our forum. The forum is the primary support and feature discussions medium.

Of course, all updates of Highlight Plus will be eventually available on the Asset Store.



More Cool Assets!
-----------------
Check out our other assets here:
https://assetstore.unity.com/publishers/15018



Version history
---------------

Version 3.2.4
- [Fix] Fixed flickering issue when combining mesh & skinned mesh renderers

Version 3.2.3
- General improvements and fixes

Version 3.2.2
- [Fix] Fixed issue when trying to read normals from a non-readable mesh

Version 3.2.1
- [Fix] Fixed glow disappearing when object crosses camera near clip

Version 3.2
- Added "Reflection Probes" option
- Internal improvements and fixes

Version 3.1
- Added "Constant Width" option to Glow/Outline in Fastest/High quality level
- Added "Optimal Blit" option to Glow/Outline with Debug View

Version 3.0.2
- API: added proxy methods ProfileLoad, ProfileReload, ProfileSaveChanges to load/store profile settings at runtime. You can also load/save changes between effect and profile using the Load/Save methods on the profile object itself.

Version 3.0.1
- [Fix] Fixed an exception when glow was enabled, outline disabled in highest quality level

Version 3.0
- Added HQ Highest quality glow and outline options
- Added "Include" option to choose which objects are affected by the effects (same gameobject, children, root to children, or many objects in layer)
- Added "Alpha CutOff"

Version 2.6.1
- Minor internal improvements

Version 2.6
- Added Target effect
- Improved performance on Skinned Mesh Renderers. Slightly improved performance on normal renderers.

Version 2.5.2
- [Fix] Fixed issue with HQ Outer Glow not showing when there's multiple selected objects parented to the same object

Version 2.5.1
- Added support for orthographic camera

Version 2.5
- Added support for VR Single Pass Instanced
- Minor improvements and fixes

Version 2.4
- New HighlightSeeThroughOccluder script. Add it to any object to cancel any see-through effect
- Added "Fade In Duration" / "Fade Out Duration" to create smooth transition states
- Added "Glow HQ" to produce better outer glow on certain shapes
- Added "OnRendererHighlightStart" event
- API: added "OverlayOnShot" method for impact effects

Version 2.3
- Added "Raycast Source" to Highlight Trigger and Manager components
- Added "Skinned Mesh Bake Mode" to optimize highlight on many models

Version 2.2
- Added "Always On Top" option to Outline, Outer and Inner Glow
- Added "Trigger Mode" to Highlight Trigger to support complex objects

Version 2.1
- Added "Outline HQ" to inspector. Creates a better outline on certain shapes
- Added "Ignore Object Visibility" to enable effects on disabled renderers or hidden objects

Version 2.0
- Profiles. Store/load/share settings across different objects.
- [Fix] Fixed issue when copying component values between two objects
- [Fix] Fixed effects ignoring culling mask on additional cameras

Version 1.5
- Added "Inner Glow" effect

Version 1.4
- Added "Overlay Min Intensity" and "Overlay Blending" options
- Added "Ignore" option
- Minor improvements & fixes

Version 1.3
- Added option to add depth compatibility for transparent shaders

Version 1.2.4
- [Fix] Fix for multiple skinned models
- [Fix] Fix for scaled skinned models

Version 1.2.3
- [Fix] Fixes for Steam VR

Version 1.2.1
- Internal improvements and fixes

Version 1.2.1
- [Fix] Fixed script execution order issue with scripts changing transform in LateUpdate()

Version 1.2
- Support for LOD groups

Version 1.1
- Redesigned editor inspector
- Minor improvements

Version 1.0.4
- Supports meshes with negative scales

Version 1.0.3
- Support for multiple submeshes

Version 1.0.2
- [Fix] Fixed scale issue with grouped objects

Version 1.0.1
- Supports combined meshes

Version 1.0 - Nov/2018
- Initial release
