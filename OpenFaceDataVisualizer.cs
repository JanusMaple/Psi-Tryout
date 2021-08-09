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
        static Visualizer visualizer;
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
                var openFaceImageAddress = "C:\\Users\\Janus\\Desktop\\Summer Internship\\Psi\\Image";

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
                var openFaceImageStream = decodedImageStream
                    .Where((img, e) => e.SequenceId % 3 == 0)
                    .Select(img => opanFaceProcess(img, openFaceImageAddress));
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

        static void openFaceSetup()
        {
            //Record settings
            RecordAligned = false; // Aligned face images
            RecordHOG = false; // HOG features extracted from face images
            Record2DLandmarks = true; // 2D locations of facial landmarks (in pixels)
            Record3DLandmarks = true; // 3D locations of facial landmarks (in pixels)
            RecordModelParameters = true; // Facial shape parameters (rigid and non-rigid geometry)
            RecordPose = true; // Head pose (position and orientation)
            RecordAUs = true; // Facial action units
            RecordGaze = true; // Eye gaze
            RecordTracked = true; // Recording tracked videos or images

            //Visualize setting
            ShowTrackedVideo = true; // Showing the actual tracking
            ShowAppearance = true; // Showing appearance features like HOG
            ShowGeometry = true; // Showing geometry features, pose, gaze, and non-rigid
            ShowAUs = true; // Showing Facial Action Units
            visualizer = new Visualizer(ShowTrackedVideo || RecordTracked, ShowAppearance, ShowAppearance, false);

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

        static void RecordObservation(RecorderOpenFace recorder, RawImage vis_image, int face_id, bool success, float fx, float fy, float cx, float cy, double timestamp, int frame_number)
        {

            recorder.SetObservationTimestamp(timestamp);

            double confidence = landmark_detector.GetConfidence();

            List<float> pose = new List<float>();
            landmark_detector.GetPose(pose, fx, fy, cx, cy);
            recorder.SetObservationPose(pose);

            List<Tuple<float, float>> landmarks_2D = landmark_detector.CalculateAllLandmarks();
            List<Tuple<float, float, float>> landmarks_3D = landmark_detector.Calculate3DLandmarks(fx, fy, cx, cy);
            List<float> global_params = landmark_detector.GetRigidParams();
            List<float> local_params = landmark_detector.GetNonRigidParams();

            recorder.SetObservationLandmarks(landmarks_2D, landmarks_3D, global_params, local_params, confidence, success);

            var gaze = gaze_analyser.GetGazeCamera();
            var gaze_angle = gaze_analyser.GetGazeAngle();

            var landmarks_2d_eyes = landmark_detector.CalculateAllEyeLandmarks();
            var landmarks_3d_eyes = landmark_detector.CalculateAllEyeLandmarks3D(fx, fy, cx, cy);
            recorder.SetObservationGaze(gaze.Item1, gaze.Item2, gaze_angle, landmarks_2d_eyes, landmarks_3d_eyes);

            var au_regs = face_analyser.GetCurrentAUsReg();
            var au_classes = face_analyser.GetCurrentAUsClass();
            recorder.SetObservationActionUnits(au_regs, au_classes);

            recorder.SetObservationFaceID(face_id);
            recorder.SetObservationFrameNumber(frame_number);

            recorder.SetObservationFaceAlign(face_analyser.GetLatestAlignedFace());

            var hog_feature = face_analyser.GetLatestHOGFeature();
            recorder.SetObservationHOG(success, hog_feature, face_analyser.GetHOGRows(), face_analyser.GetHOGCols(), face_analyser.GetHOGChannels());

            recorder.SetObservationVisualization(vis_image);

            recorder.WriteObservation();


        }

        static void VisualizeFeatures(RawImage frame, Visualizer visualizer, List<Tuple<float, float>> landmarks, List<bool> visibilities, bool detection_succeeding,
            bool new_image, bool multi_face, float fx, float fy, float cx, float cy)
        {
            List<Tuple<Point, Point>> lines = null;
            List<Tuple<float, float>> eye_landmarks = null;
            List<Tuple<Point, Point>> gaze_lines = null;
            Tuple<float, float> gaze_angle = new Tuple<float, float>(0, 0);

            List<float> pose = new List<float>();
            landmark_detector.GetPose(pose, fx, fy, cx, cy);

            double confidence = landmark_detector.GetConfidence();

            if (confidence < 0)
                confidence = 0;
            else if (confidence > 1)
                confidence = 1;

            // Helps with recording and showing the visualizations
            if (new_image)
            {
                visualizer.SetImage(frame, fx, fy, cx, cy);
            }
            visualizer.SetObservationHOG(face_analyser.GetLatestHOGFeature(), face_analyser.GetHOGRows(), face_analyser.GetHOGCols());
            visualizer.SetObservationLandmarks(landmarks, confidence, visibilities);
            visualizer.SetObservationPose(pose, confidence);
            visualizer.SetObservationGaze(gaze_analyser.GetGazeCamera().Item1, gaze_analyser.GetGazeCamera().Item2, landmark_detector.CalculateAllEyeLandmarks(), landmark_detector.CalculateAllEyeLandmarks3D(fx, fy, cx, cy), confidence);

            eye_landmarks = landmark_detector.CalculateVisibleEyeLandmarks();
            lines = landmark_detector.CalculateBox(fx, fy, cx, cy);

            gaze_lines = gaze_analyser.CalculateGazeLines(fx, fy, cx, cy);
            gaze_angle = gaze_analyser.GetGazeAngle();
        }

        static Shared<Image> opanFaceProcess(Shared<Image> img, string openFaceImageAddress)
        {
            //Get the webcam image and set intrinsics settings
            img.Resource.ToBitmap().Save(openFaceImageAddress + "\\samples\\Image.jpg");
            cx = img.Resource.Width / 2.0f;
            cy = img.Resource.Height / 2.0f;
            fx = fy = (500.0f * (img.Resource.Width / 640.0f) + 500.0f * (img.Resource.Height / 480.0f)) / 2.0f;
            var imageReader = new ImageReader(openFaceImageAddress + "\\samples", fx, fy, cx, cy);
            var image = imageReader.GetNextImage();
            var gray_image = imageReader.GetCurrentFrameGray();
            visualizer.SetImage(image, fx, fy, cx, cy);

            //Record settings
            RecorderOpenFaceParameters rec_params = new RecorderOpenFaceParameters(false, false,
                    Record2DLandmarks, Record3DLandmarks, RecordModelParameters, RecordPose, RecordAUs,
                    RecordGaze, RecordHOG, RecordTracked, RecordAligned, true,
                    fx, fy, cx, cy, 0);

            RecorderOpenFace recorder = new RecorderOpenFace(imageReader.GetName(), rec_params, openFaceImageAddress + "\\processed");

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

            //Get the processed image
            for (int i = 0; i < face_detections.Count; ++i)
            {
                bool detection_succeeding = landmark_detector.DetectFaceLandmarksInImage(image, face_detections[i], face_model_params, gray_image);

                var landmarks = landmark_detector.CalculateAllLandmarks();

                // Predict action units
                var au_preds = face_analyser.PredictStaticAUsAndComputeFeatures(image, landmarks);

                // Predic eye gaze
                gaze_analyser.AddNextFrame(landmark_detector, detection_succeeding, fx, fy, cx, cy);

                // Only the final face will contain the details
                VisualizeFeatures(image, visualizer, landmarks, landmark_detector.GetVisibilities(), detection_succeeding, i == 0, true, fx, fy, cx, cy);

                // Record an observation
                RecordObservation(recorder, visualizer.GetVisImage(), i, detection_succeeding, fx, fy, cx, cy, 0, 0);

            }

            recorder.SetObservationVisualization(visualizer.GetVisImage());

            image = imageReader.GetNextImage();
            gray_image = imageReader.GetCurrentFrameGray();

            // Write out the tracked image
            if (RecordTracked)
            {
                recorder.WriteObservationTracked();
            }

            // Do not carry state accross images
            landmark_detector.Reset();
            face_analyser.Reset();
            recorder.Close();

            //Read the processed image
            System.Drawing.Bitmap newBitMap = new System.Drawing.Bitmap(openFaceImageAddress + "\\processed\\Image.jpg");
            Image newImg = Image.FromBitmap(newBitMap);
            newImg.CopyFrom(newBitMap);
            Shared<Image> newSharedImg = Shared.Create(newImg);
            return newSharedImg;
        }
    }
}
