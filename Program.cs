/*
A demo program showing how to use Psi pipeline record and save videos
*/

using System;
using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Media;

namespace Psi_Project
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var p = Pipeline.Create(enableDiagnostics: true))
            {
                //Specify File Names and Address
                var storeName = "demo";
                var storeAddress = "C:\\Users\\Janus\\Desktop\\Summer Internship\\Psi";


                // Create the store
                var store = PsiStore.Create(p, storeName, storeAddress);

                // Create the webcam and write its output to the store as compressed JPEGs
                var webcam = new MediaCapture(p, 640, 480, 30);
                webcam.Out.EncodeJpeg(90, DeliveryPolicy.LatestMessage).Write("Image", store);

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
