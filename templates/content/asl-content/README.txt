MyMod - ASL content mod (no code)
=================================

This folder is a content mod: a manifest.json plus image files. ASL swaps game textures for
your PNGs when a level loads. No compiler needed.

1. Add your image
   Put a PNG in this folder, for example my-texture.png. Use an 8-bit, non-interlaced PNG
   (grayscale, RGB, palette, gray+alpha or RGBA). The file is NOT included in the template.

2. Find a texture name to replace
   manifest.json already has "listTextureNames": true. Start the game once, then open
   BepInEx\LogOutput.log and search for "texture". ASL logs a sample of the loaded texture
   names together with a "readable" flag for each one. Pick the name you want to replace.

   Readable textures are rewritten in place, so every material using them updates at once.
   Non-readable textures are swapped by reference (materials, UI sprites and RawImage textures
   that used the old one are repointed).

3. Point the manifest at it
   Edit manifest.json:

     "textures": [
       { "target": "NAME_FROM_LOG", "file": "my-texture.png" }
     ]

   "target" is the texture name from the log. "file" is the PNG in this folder.

4. Test
   Restart the game (or change scene) and look at the result. If the log says
   "texture file not found", the "file" value does not match a file in this folder.
   Note: "star_gray" is only a placeholder name from the ASL sample mod; replace it.

Once it works, you can rename the folder, change "id" (com.yourname.mymod) and "author",
and ship the folder as a zip. Players copy it into the game's mods\ folder.
