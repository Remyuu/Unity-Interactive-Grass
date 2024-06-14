# Unity-Interactive-Grass with Quad Tree
 Using DrawMeshInstancedIndirect Draw 100000+ Grass  

## Setup

1. Create an empty object in your Unity scene. Name it `Grass Generator - Holder`.
2. Place the `GrassPainter.cs` script into the Editor folder of your project. This is necessary for the script to function properly in the Unity Editor.
3. Attach the `GrassControl.cs` script to the `Grass Generator - Holder` object you created in Step 1.
4. Select the object(s) where you want to generate grass.
With the object(s) selected, go to the GrassControl component on your Grass Generator - Holder object and click the Generate Grass to Selected Objects button.


## Snapshot
 
<img width="718" alt="image" src="https://github.com/Remyuu/Unity-Interactive-Grass/assets/64857501/fa43ea22-15f3-42be-b36b-b89714c4ac38">
<img width="770" alt="image" src="https://github.com/Remyuu/Unity-Interactive-Grass/assets/64857501/2d97e67f-d2fa-41b4-8787-1add30e6ba20">
<img width="656" alt="image" src="https://github.com/Remyuu/Unity-Interactive-Grass/assets/64857501/a33440b9-8233-4586-80ef-a573dde197d2">
<img width="655" alt="image" src="https://github.com/Remyuu/Unity-Interactive-Grass/assets/64857501/5a415960-cc03-444f-9641-985921bf7435">

Key code:

```csharp
Graphics.DrawMeshInstancedIndirect(blade, 0, m_Material, m_LocalBounds, m_argsBuffer);
```

Reference:
1. https://patreon.com/minionsart?utm_medium=clipboard_copy&utm_source=copyLink&utm_campaign=creatorshare_fan&utm_content=join_link
2. https://github.com/ColinLeung-NiloCat/UnityURP-MobileDrawMeshInstancedIndirectExample
