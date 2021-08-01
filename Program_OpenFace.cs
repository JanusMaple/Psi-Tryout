/*
Use external executable OpenFace binaries to process images captured by Psi pipeline
*/

using System;
using System.Diagnostics;
using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Media;

namespace Psi_Project
{
    class Program
    {
        static Shared<Image> opanFaceProcess(Shared<Image> img, string webcamImageAddress, string openFaceImageAddress, string excutableOpenFaceAddress, string openFaceWorkingDirectory)
        {
            //Save the webcam image
            img.Resource.ToBitmap().Save(webcamImageAddress);

            //Use OpenFace to process the webcam image
            Process cmd = new Process();
            cmd.StartInfo.FileName = excutableOpenFaceAddress;
            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardError = true;
            cmd.StartInfo.WorkingDirectory = openFaceWorkingDirectory;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.Arguments = "-f" + " \"" + webcamImageAddress + "\"";
            cmd.Start();
            cmd.WaitForExit();

            //Read the processed image
            System.Drawing.Bitmap newBitMap = new System.Drawing.Bitmap(openFaceImageAddress);
            Image newImg = Image.FromBitmap(newBitMap);
            newImg.CopyFrom(newBitMap);
            Shared<Image> newSharedImg = Shared.Create(newImg);
            return newSharedImg;
        }

        static void Main(string[] args)
        {
            using (var p = Pipeline.Create(enableDiagnostics: true))
            {
                //Specify File Names and Address
                var storeName = "demo";
                var storeAddress = "C:\\Users\\Janus\\Desktop\\Summer Internship\\Psi";
                var webcamImageAddress = storeAddress + "\\Webcam Image\\Image.jpg";
                var openFaceWorkingDirectory = "D:\\OpenFace\\OpenFace_2.2.0_win_x64";
                var excutableOpenFaceAddress = openFaceWorkingDirectory + "\\FaceLandmarkImg.exe";
                var openFaceImageAddress = openFaceWorkingDirectory + "\\processed\\Image.jpg";

                // Create the store
                var store = PsiStore.Create(p, storeName, storeAddress);

                // Create the webcam and write its output to the store as compressed JPEGs
                var webcam = new MediaCapture(p, 640, 480, 30);
                var imageStream = webcam.Out.EncodeJpeg(90, DeliveryPolicy.LatestMessage);
                imageStream.Write("Image", store);

                //Use OpenFace to process images
                var decodedImageStream = imageStream.Decode();
                var openFaceImageStream = decodedImageStream.Select(img => opanFaceProcess(img, webcamImageAddress, openFaceImageAddress, excutableOpenFaceAddress, openFaceWorkingDirectory));
                openFaceImageStream.Write("OpenFaceImage", store);

                // Create the AudioCapture component and write the output to the store
                var audioCaptureConfiguration = new AudioCaptureConfiguration() { Format = WaveFormat.Create16kHz1Channel16BitPcm() };
                var audio = new AudioCapture(p, audioCaptureConfiguration);
                audio.Write("Audio", store);

                // Write the diagnostics stream to the store
                p.Diagnostics.Write("Diagnostics", store);

                // Run the pipeline
                p.RunAsync();

                Console.WriteLine("Press any key to finish recording");
                Console.ReadKey();
            }
        }
    }
}
