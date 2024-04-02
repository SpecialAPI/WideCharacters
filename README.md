
# WideCharacters
A library for making wide characters in Brutal Orchestra, as well as an example of its usage.

# Library Installation
To download install this library for your mod, follow these steps:
 1. Download the code on GitHub. This can be done by clicking Code -> Download ZIP.  
![](https://raw.githubusercontent.com/SpecialAPI/WideCharacters/main/ImagesForReadme/download.png)
 2. After downloading the zip with the code, extract it somewhere. Don't extract it into your project's folder or any of its subfolders though.
 3. After extracting the zip, add all of the `.cs` files from the `WideCharacters-main/API` folder. This can be done by right clicking your project in Visual Studio and then doing Add -> Existing Item and then selecting the files.  
![](https://raw.githubusercontent.com/SpecialAPI/WideCharacters/main/ImagesForReadme/additem.png)  
![](https://raw.githubusercontent.com/SpecialAPI/WideCharacters/main/ImagesForReadme/additem2.png)
 4. After adding the files, there's still some things you need to do. Firstly, you need to add the line `new Harmony({your mod's guid goes here}).PatchAll()` somewhere in your plugin's (plugin is the class that has `: BaseUnityPlugin` and the `Awake()` method where you add all of your content) `Awake()` method. Your mod's guid is the first string that goes into the `BaseUnityPlugin` directly above the line that has `: BaseUnityPlugin`.  
![](https://raw.githubusercontent.com/SpecialAPI/WideCharacters/main/ImagesForReadme/harmony.png)
 5. Secondly, you need to change the `MOD_GUID` constant in the `CharacterSpriteScalePatches.cs` file. Its value must match your mod's guid.  
![](https://raw.githubusercontent.com/SpecialAPI/WideCharacters/main/ImagesForReadme/guid.png)
 6. After doing that, the setup is done. If you want to see how to actually make a wide character, you can look in the [ExampleWideCharacter.cs](https://github.com/SpecialAPI/WideCharacters/blob/main/Example/ExampleWideCharacter.cs) file