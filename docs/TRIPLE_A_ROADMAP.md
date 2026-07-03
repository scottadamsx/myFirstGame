# Improvements & Triple-A Roadmap

## Recent Improvements

We have significantly overhauled the environment and world generation to create a much more playable and aesthetic foundation:

*   **Procedural Road Generation**: Replaced the original low-poly, bumpy FBX roads with a custom procedural road generator (`OSMRoadGenerator`).
    *   Reads original accurate spatial data from `stjohns_game_data.json`.
    *   Utilizes `CoordinateMapper` to translate Blender spatial coordinates perfectly into the Unity world space.
    *   Dynamically raycasts the road splines against the terrain and suspends the generated mesh exactly 0.45m above the ground, eliminating grass clipping and z-fighting.
*   **Visual Upgrades System**: Implemented `VisualUpgrade.cs` to handle post-processing and material swapping at runtime, preventing destructive edits to the original FBX.
    *   **Material Overrides**: Automatically applies crisp, tiled textures (grass, asphalt, concrete, and building facades) to the flat geometry.
    *   **Elevation Fixes**: Automatically lifts buildings (`+0.4f` Y-offset) to ensure they sit flush against the newly elevated procedural roads.
    *   **Post-Processing**: Added a Filmic Volume Profile (Bloom, ACES Tonemapping, Color Adjustments, Vignette) to give the game a cinematic look.

---

## The Path to Triple-A (AAA) Quality

To evolve this project from a stylized prototype into a photorealistic, Triple-A open-world experience, the following areas require investment:

### 1. Rendering & Lighting
*   **High Definition Render Pipeline (HDRP)**: Migrate from URP to Unity's HDRP for state-of-the-art lighting, volumetric effects, and physical light units.
*   **Volumetric Fog & Weather**: St. John's is defined by its weather. Implement volumetric fog that settles in the harbor and reacts to light. Add dynamic rain systems with wet-surface shaders (puddles, reflection probes).
*   **Global Illumination (GI)**: Implement real-time GI or high-density Light Probes to ensure buildings bounce light onto the streets realistically.

### 2. Environment & Texturing
*   **PBR Materials**: Move away from flat vertex colors to Physically Based Rendering (PBR) materials. Every building needs a Diffuse, Normal, Roughness, and Metallic map.
*   **Terrain Splat-mapping**: Instead of a uniform grass texture, the terrain needs a shader that blends rock, dirt, and grass based on the slope angle and noise maps.
*   **Decals**: Use projectors/decals to add road wear, potholes, lane markings, and graffiti to break up the repeating textures.
*   **Foliage**: Integrate SpeedTree for realistic, wind-reactive trees, and use GPU instancing for dense, high-quality grass on the hills.

### 3. Physics & Gameplay
*   **Advanced Vehicle Physics**: Replace the basic `ArcadeCar` physics with a professional tire-friction model (like *Edy's Vehicle Physics* or *Vehicle Physics Pro*). You need proper suspension, tire slip, and RPM simulation for AAA driving.
*   **World Streaming (Addressables)**: A AAA city cannot load all at once. Break the city into chunks (e.g., Downtown, Signal Hill) and use Unity's Addressables/Scene Streaming to load/unload geometry dynamically as the player drives.

### 4. Audio
*   **Spatial Audio System**: Integrate FMOD or Wwise. 
*   **Soundscapes**: Add directional wind, the sound of seagulls near the harbor, ocean waves crashing, and realistic engine RPM audio that shifts gears dynamically.

### 5. Living World (AI)
*   **Traffic & Pedestrians**: Use Unity DOTS (Data-Oriented Technology Stack) or ECS to simulate hundreds of cars and pedestrians without tanking the framerate.
