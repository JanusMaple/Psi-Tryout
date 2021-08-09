# Psi-Tryout
Try using Psi to record videos and analyze videos with OpenFace. 
## Dependencies
Nu-Packages (Prerelease): 

    Microsoft.Psi.Runtime
    Microsoft.Psi.Media.Windows.x64
    Microsoft.Psi.Imaging.Windows
    Microsoft.Psi.Audio.Windows

## Prerequisites
Visual Studio 2019 (Recommended)

.NET Framework 4.7.2 (Recommended)

Windows x64 (Platform taget should be x64)

## Function
Capture videos and sounds and save them in Psi store files. (For example demo.Catalog_000000.psi, demo.Data_000000.psi and demo.Index_000000.psi)

## Post-Process
Need PsiStudio (https://github.com/microsoft/psi/wiki/Building-the-Codebase) to read and visualize stored data. 

## Program.cs
A demonstration program of Psi

## Program_OpenFace.cs
A demonstration program of integrating OpenFace binaries into Psi, which can not work well since the program need to reload all model parameters for frame captured by camera

## OpenFaceDataExtractor.cs
This program need to be built under the project `OpenFaceOffline`. The startup object need to be `Psi_Project.Program` and the output type should be changed to `Console application` instead of `Windows application`. 

This program can run well and output data produced by OpenFace.

## OpenFaceVisualizer.cs
This program need to be built under the project `OpenFaceOffline`. The startup object need to be `Psi_Project.Program` and the output type should be changed to `Console application` instead of `Windows application`. 

`RecorderOpenFace` and `Visualizer` class are used to realize visualization of OpenFace processed images, which can not work well since the `RecorderOpenFace` only have interface to process images by read&write files in the disk. 
