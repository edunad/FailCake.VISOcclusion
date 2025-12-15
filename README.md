<h2>
	<p align="center">
		FailCake.VISOcclusion<br/><br/>
		<img width="256" height="256" src="https://i.rawr.dev/ezgif-734d0bbaa3250a77.gif" />
	</p>
</h2>
<h4>
	<p align="center">
		<a href="/LICENSE"><img alt="logo" src="https://img.shields.io/github/license/edunad/FailCake.VISOcclusion"/>&nbsp;</a>
		<a href="https://github.com/edunad/FailCake.VISOcclusion/issues?q=is%3Aopen+is%3Aissue+label%3ABUG"><img alt="logo" src="https://img.shields.io/github/issues/edunad/FailCake.VISOcclusion/BUG.svg"/>&nbsp;</a>
		<a href="https://github.com/edunad/FailCake.VISOcclusion/commits/master/"><img alt="logo" src="https://img.shields.io/github/last-commit/edunad/FailCake.VISOcclusion.svg"/>&nbsp;</a><br/>
		<br/>
		<a href="#installation">Installation</a> -
		<a href="#features">Features</a> -
		<a href="#sample">Sample</a> -
		<a href="#tutorial">Tutorial</a>
	</p>
</h4>

> [!IMPORTANT]
> Originally built for a game made by a friend [Delivery & Beyond](https://store.steampowered.com/app/3376480/Delivery__Beyond/), not really intended for public use. But after thinking about it, figured I'd share it anyway. Some stuff is still hardcoded for the game, but eventually I want to turn this into a proper plugin.

> [!IMPORTANT]
> Single camera only! Should be your main player camera and be marked with the `MainCamera` tag!

## Installation
<img width="256" height="256" src="https://github.com/user-attachments/assets/5d3fcc53-d3b5-4938-94b6-9974f772c646" />

Package Manager > Add Git URL:
```
https://github.com/edunad/FailCake.VISOcclusion.git?path=/com.failcake.vis.occlusion
```

## Features
- Compute shaders for fast visibility checks. No Jobs! All calculations done on the GPU side.
- Works with 2D and 3D portals (3D just does frustum culling, 2D does the full visibility check)

## Sample
> TODO: Add sample scene

## Tutorial

1. Add the VISOcclusion render feature to your scriptable renderer
   
<p align="center">
<img width="256" height="256" alt="{83B22150-529F-4244-9DC8-93E4C707866C}" src="https://github.com/user-attachments/assets/1746f846-5186-43a2-89d6-2ce162777c33" />
</p>

2. Make a GameObject with `VISController` script. This handles all the room show/hide logic.
   
<p align="center">
<img width="256" height="256" alt="{C6063E7C-D2F0-4629-A1D8-8A0B16264684}" src="https://github.com/user-attachments/assets/4ccd15c0-c9ba-4398-8b73-e0cbb2512948" />
<br/>
<img width="256" height="256" alt="{39DF731D-0E3F-4B92-BDEE-B77976D083B0}" src="https://github.com/user-attachments/assets/d17b0ca8-b66c-4ef9-8054-8e8f854b108a" />
</p>

3. Create another GameObject with `entity_vis_room`, `entity_vis_test` and a `BoxCollider` (set to trigger). The `entity_vis_test` is just an example script showing how to handle room visibility.
   
<p align="center">
<img width="256" height="256" alt="{2BE76E7A-60FD-49F3-8391-99A6DE34D046}" src="https://github.com/user-attachments/assets/511d9350-69f0-46e6-960a-0e87816fb40e" />
</p>

4. Add a GameObject with `entity_vis_portal_2d`. Put it where your doorway is and link it to the room on the right side.
   
<p align="center">
<img width="256" height="256" alt="{84A72654-8B28-485F-9ACF-5F105DCE25B8}" src="https://github.com/user-attachments/assets/d0eb284b-99f9-4338-bf0b-7a0ca75e1ef5" />
</p>

5. DONE

## Debugging
If stuff isn't working, turn on `DEBUG` mode in VISController to see the portal occlusion visualization.
