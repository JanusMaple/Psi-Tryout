using System;
using System.Windows;
using System.Collections.Generic;

//Psi libraries
using Microsoft.Psi;
using Microsoft.Psi.Audio;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Media;

//Internal libraries
using OpenCVWrappers;
using CppInterop.LandmarkDetector;
using FaceAnalyser_Interop;
using GazeAnalyser_Interop;
using FaceDetectorInterop;
using UtilitiesOF;


namespace Psi_Project
{
    class Program
    {
        static bool ShowTrackedVideo, ShowAppearance, ShowGeometry, ShowAUs;
        static bool RecordAligned, RecordHOG, Record2DLandmarks, Record3DLandmarks, RecordModelParameters, RecordPose, RecordAUs, RecordGaze, RecordTracked;
        static bool LandmarkDetectorCLM, LandmarkDetectorCLNF, LandmarkDetectorCECLM, DetectorHaar, DetectorHOG, DetectorCNN, maskAligned;
        static FaceModelParameters face_model_params;
        static FaceDetector face_detector;
        static CLNF landmark_detector;
        static GazeAnalyserManaged gaze_analyser;
        static FaceAnalyserManaged face_analyser;
        static int imageOutputSize = 112;
        static float cx, cy;   //Optical Center
        static float fx, fy;   //Focal Length
        static string root = AppDomain.CurrentDomain.BaseDirectory;

        static void Main(string[] args)
        {
            using (var p = Pipeline.Create(enableDiagnostics: true))
            {
                //Specify File Names and Address
                var storeName = "demo";
                var storeAddress = "C:\\Users\\Janus\\Desktop\\Summer Internship\\Psi";

                //Set up for OpenFace
                openFaceSetup();

                // Create the Psi store
                var store = PsiStore.Create(p, storeName, storeAddress);

                // Create the webcam and write its output to the store as compressed JPEGs
                var webcam = new MediaCapture(p, 640, 480, 30);
                var imageStream = webcam.Out.EncodeJpeg(90, DeliveryPolicy.LatestMessage);
                imageStream.Write("Image", store);

                //Use OpenFace to process images and write images with landmarks to the store
                var decodedImageStream = imageStream.Decode();
                var openFaceDataStream = decodedImageStream
                    .Where((img, e) => e.SequenceId % 3 == 0)
                    .Select(img => opanFaceProcess(img));
                openFaceDataStream.Write("GazeAngle_1", store);

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

        static void openFaceSetup()
        {
            //Landmark detector selector
            LandmarkDetectorCLM = false;
            LandmarkDetectorCLNF = false;
            LandmarkDetectorCECLM = true;

            //Initialize landmark detector
            face_model_params = new FaceModelParameters(root, LandmarkDetectorCECLM, LandmarkDetectorCLNF, LandmarkDetectorCLM);

            //Face detector selector
            DetectorHaar = false;
            DetectorHOG = false;
            DetectorCNN = true;

            //Initialize face detector
            face_detector = new FaceDetector(face_model_params.GetHaarLocation(), face_model_params.GetMTCNNLocation());

            //Use HOG if CNN is not loaded
            if (!face_detector.IsMTCNNLoaded())
            {
                DetectorCNN = false;
                DetectorHOG = true;
            }
            face_model_params.SetFaceDetector(DetectorHaar, DetectorHOG, DetectorCNN);

            //Create landmark detector, gaze analyser and face analyser
            landmark_detector = new CLNF(face_model_params);
            gaze_analyser = new GazeAnalyserManaged();
            maskAligned = true;
            face_analyser = new FaceAnalyserManaged(root, false, imageOutputSize, maskAligned);
        }

        static System.Drawing.Bitmap toGrayBitmap(System.Drawing.Bitmap image)
        {
            System.Drawing.Color color;
            int gray_value;
            for (int i = 0; i < image.Width; i++)
            {
                for (int j = 0; j < image.Height; j++)
                {
                    color = image.GetPixel(i, j);
                    gray_value = (int)(color.R * 0.299 + color.G * 0.587 + color.B * 0.114);
                    image.SetPixel(i, j, System.Drawing.Color.FromArgb(gray_value, gray_value, gray_value));
                }
            }
            return image;
        }

        static float opanFaceProcess(Shared<Image> img)
        {
            //Get the webcam image and set intrinsics settings
            var image = new RawImage(img.Resource.ToBitmap());
            var gray_image = new RawImage(toGrayBitmap(img.Resource.ToBitmap()));
            cx = img.Resource.Width / 2.0f;
            cy = img.Resource.Height / 2.0f;
            fx = fy = (500.0f * (img.Resource.Width / 640.0f) + 500.0f * (img.Resource.Height / 480.0f)) / 2.0f;

            //Optimize for video
            face_model_params.optimiseForVideo();

            // Detect faces here and return bounding boxes
            List<Rect> face_detections = new List<Rect>();
            List<float> confidences = new List<float>();
            if (DetectorHOG)
            {
                face_detector.DetectFacesHOG(face_detections, gray_image, confidences);
            }
            else if (DetectorCNN)
            {
                face_detector.DetectFacesMTCNN(face_detections, image, confidences);
            }
            else if (DetectorHaar)
            {
                face_detector.DetectFacesHaar(face_detections, gray_image, confidences);
            }

            List<Tuple<float, float>> eye_landmarks = null;
            List<Tuple<float, float>> landmarks_2d_eyes = null;
            List<Tuple<float, float, float>> landmarks_3d_eyes = null;
            Tuple<float, float> gaze_angle = new Tuple<float, float>(0, 0);
            Dictionary<string, double> au_regs = null;
            Dictionary<string, double> au_classes = null;
            List<float> pose = new List<float>();
            double confidence = 0.0;

            //Get the processed image
            for (int i = 0; i < face_detections.Count; ++i)
            {
                //Find all landmarks
                bool detection_succeeding = landmark_detector.DetectFaceLandmarksInImage(image, face_detections[i], face_model_params, gray_image);

                var landmarks = landmark_detector.CalculateAllLandmarks();

                // Predict action units
                var au_preds = face_analyser.PredictStaticAUsAndComputeFeatures(image, landmarks);

                // Predic eye gaze
                gaze_analyser.AddNextFrame(landmark_detector, detection_succeeding, fx, fy, cx, cy);

                //Get Prediction Confidence
                confidence = landmark_detector.GetConfidence();

                //Predict pose
                landmark_detector.GetPose(pose, fx, fy, cx, cy);

                //Find eye landmarks
                eye_landmarks = landmark_detector.CalculateVisibleEyeLandmarks();
                landmarks_2d_eyes = landmark_detector.CalculateAllEyeLandmarks();
                landmarks_3d_eyes = landmark_detector.CalculateAllEyeLandmarks3D(fx, fy, cx, cy);

                //Get gaze angles
                gaze_angle = gaze_analyser.GetGazeAngle();

                //Get AU results
                au_regs = face_analyser.GetCurrentAUsReg();
                au_classes = face_analyser.GetCurrentAUsClass();
            }

            // Do not carry state accross images
            landmark_detector.Reset();
            face_analyser.Reset();

            //Return openface data
            return (gaze_angle.Item1);
        }
    }
}
