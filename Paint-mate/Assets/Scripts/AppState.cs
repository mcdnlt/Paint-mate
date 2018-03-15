﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using HoloToolkit.Unity;
using HoloToolkit.Unity.InputModule;
using HoloToolkit.Unity.SpatialMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_WSA || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

namespace HoloToolkit.Examples.SpatialUnderstandingFeatureOverview
{
    public class AppState : Singleton<AppState>, ISourceStateHandler, IInputClickHandler
    {
        // Consts
        public float kMinAreaForStats = 5.0f;
        public float kMinAreaForComplete = 50.0f;
        public float kMinHorizAreaForComplete = 25.0f;
        public float kMinWallAreaForComplete = 10.0f;

        // Config
        public TextMesh DebugDisplay;
        public TextMesh DebugSubDisplay;
        public Transform Parent_Scene;
        public SpatialMappingObserver MappingObserver;
        public SpatialUnderstandingCursor AppCursor;

		// PaintMate
		public GameObject lookAroundPrefab;
		public GameObject PaintMateAreaDisplayPrefab;
		public GameObject newMenuBit;
		public GameObject newMenuBit2;

        // Properties
        public string SpaceQueryDescription
        {
            get
            {
                return spaceQueryDescription;
            }
            set
            {
                spaceQueryDescription = value;
                objectPlacementDescription = "";
            }
        }

        public string ObjectPlacementDescription
        {
            get
            {
                return objectPlacementDescription;
            }
            set
            {
                objectPlacementDescription = value;
                spaceQueryDescription = "";
            }
        }

        public bool DoesScanMeetMinBarForCompletion
        {
            get
            {
                // Only allow this when we are actually scanning
                if ((SpatialUnderstanding.Instance.ScanState != SpatialUnderstanding.ScanStates.Scanning) ||
                    (!SpatialUnderstanding.Instance.AllowSpatialUnderstanding))
                {
                    return false;
                }

                // Query the current playspace stats
                IntPtr statsPtr = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStatsPtr();
                if (SpatialUnderstandingDll.Imports.QueryPlayspaceStats(statsPtr) == 0)
                {
                    return false;
                }
                SpatialUnderstandingDll.Imports.PlayspaceStats stats = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStats();

                // Check our preset requirements
                if ((stats.TotalSurfaceArea > kMinAreaForComplete) ||
                    (stats.HorizSurfaceArea > kMinHorizAreaForComplete) ||
                    (stats.WallSurfaceArea > kMinWallAreaForComplete))
                {
                    return true;
                }
                return false;
            }
        }

        public string PrimaryText
        {
            get
            {
                // Display the space and object query results (has priority)
                if (!string.IsNullOrEmpty(SpaceQueryDescription))
                {
                    return SpaceQueryDescription;
                }
                else if (!string.IsNullOrEmpty(ObjectPlacementDescription))
                {
                    return ObjectPlacementDescription;
                }

                // Scan state
                if (SpatialUnderstanding.Instance.AllowSpatialUnderstanding)
                {
                    switch (SpatialUnderstanding.Instance.ScanState)
                    {
                        case SpatialUnderstanding.ScanStates.Scanning:
                            // Get the scan stats
                            IntPtr statsPtr = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStatsPtr();
                            if (SpatialUnderstandingDll.Imports.QueryPlayspaceStats(statsPtr) == 0)
                            {
                                return "playspace stats query failed";
                            }

                            // The stats tell us if we could potentially finish
                            if (DoesScanMeetMinBarForCompletion)
                            {
                                return "When ready, air tap to complete measuring";
                            }
                            return "Walk around to scan wall for measuring";
                        case SpatialUnderstanding.ScanStates.Finishing:
                            return "Review wall measurements";
                        case SpatialUnderstanding.ScanStates.Done:
                            return "";
                        default:
                            return "ScanState = " + SpatialUnderstanding.Instance.ScanState.ToString();
                    }
                }
                return "";
            }
        }

        public Color PrimaryColor
        {
            get
            {
                if (SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.Scanning)
                {
                    if (trackedHandsCount > 0)
                    {
                        return DoesScanMeetMinBarForCompletion ? Color.green : Color.red;
                    }
                    return DoesScanMeetMinBarForCompletion ? Color.yellow : Color.white;
                }

                // If we're looking at the menu, fade it out
                Vector3 hitPos, hitNormal;
                UnityEngine.UI.Button hitButton;
                float alpha = AppCursor.RayCastUI(out hitPos, out hitNormal, out hitButton) ? 0.15f : 1.0f;

                // Special case processing & 
                return (!string.IsNullOrEmpty(SpaceQueryDescription) || !string.IsNullOrEmpty(ObjectPlacementDescription)) ?
                    (PrimaryText.Contains("processing") ? new Color(1.0f, 0.0f, 0.0f, 1.0f) : new Color(1.0f, 0.7f, 0.1f, alpha)) :
                    new Color(1.0f, 1.0f, 1.0f, alpha);
            }
        }

        public string DetailsText
        {
            get
            {
                if (SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.None)
                {
                    return "";
                }

                // Scanning stats get second priority
                if ((SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.Scanning) &&
                    (SpatialUnderstanding.Instance.AllowSpatialUnderstanding))
                {
                    IntPtr statsPtr = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStatsPtr();
                    if (SpatialUnderstandingDll.Imports.QueryPlayspaceStats(statsPtr) == 0)
                    {
                        return "Playspace stats query failed";
                    }
                    SpatialUnderstandingDll.Imports.PlayspaceStats stats = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStats();

                    // Start showing the stats when they are no longer zero
                    if (stats.TotalSurfaceArea > kMinAreaForStats)
                    {
                        string subDisplayText = string.Format("totalArea={0:0.0}, horiz={1:0.0}, wall={2:0.0}", stats.TotalSurfaceArea, stats.HorizSurfaceArea, stats.WallSurfaceArea);
                        subDisplayText += string.Format("\nnumFloorCells={0}, numCeilingCells={1}, numPlatformCells={2}", stats.NumFloor, stats.NumCeiling, stats.NumPlatform);
                        subDisplayText += string.Format("\npaintMode={0}, seenCells={1}, notSeen={2}", stats.CellCount_IsPaintMode, stats.CellCount_IsSeenQualtiy_Seen + stats.CellCount_IsSeenQualtiy_Good, stats.CellCount_IsSeenQualtiy_None);
                        return subDisplayText;
                    }
                    return "";
                }
                return "";
            }
        }

        // Privates
        private string spaceQueryDescription;
        private string objectPlacementDescription;
        private uint trackedHandsCount = 0;
#if UNITY_WSA || UNITY_STANDALONE_WIN
        private KeywordRecognizer keywordRecognizer;

        // Functions
        private void Start()
        {
            // Default the scene & the HoloToolkit objects to the camera
            Vector3 sceneOrigin = CameraCache.Main.transform.position;
            Parent_Scene.transform.position = sceneOrigin;
            MappingObserver.SetObserverOrigin(sceneOrigin);
            InputManager.Instance.AddGlobalListener(gameObject);


            var keywordsToActions = new Dictionary<string, Action>
            {
                { "Toggle Scanned Mesh", ToggleScannedMesh },
                { "Toggle Processed Mesh", ToggleProcessedMesh },
            };

            keywordRecognizer = new KeywordRecognizer(keywordsToActions.Keys.ToArray());
            keywordRecognizer.OnPhraseRecognized += args => keywordsToActions[args.text].Invoke();
            keywordRecognizer.Start();

			GameObject newMenuBit = Instantiate (lookAroundPrefab, DebugDisplay.transform.position, DebugDisplay.transform.rotation);
			newMenuBit.transform.SetParent (DebugDisplay.transform);
			newMenuBit.transform.position = DebugDisplay.transform.position;
			//DebugDisplay.transform.localScale = new Vector3 (0.01f, 0.01f, 0.01f);
			DebugDisplay.fontSize = 0;
			DebugSubDisplay.fontSize = 0;

        }
#endif

        protected override void OnDestroy()
        {
            InputManager.Instance.RemoveGlobalListener(gameObject);
        }

        private void Update_DebugDisplay(float deltaTime)
        {
            // Basic checks
            if (DebugDisplay == null)
            {
                return;
            }

            // Update display text
            DebugDisplay.text = PrimaryText;
            DebugDisplay.color = PrimaryColor;
            DebugSubDisplay.text = DetailsText;
        }

        private void Update_KeyboardInput(float deltaTime)
        {
            // Toggle SurfaceMapping & CustomUnderstandingMesh visibility
            if (Input.GetKeyDown(KeyCode.BackQuote) &&
                (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)))
            {
                ToggleScannedMesh();
            }
            else if (Input.GetKeyDown(KeyCode.BackQuote) &&
                     (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                ToggleProcessedMesh();
            }
        }

        private static void ToggleScannedMesh()
        {
            SpatialMappingManager.Instance.DrawVisualMeshes = !SpatialMappingManager.Instance.DrawVisualMeshes;
            Debug.Log("SpatialUnderstanding -> SpatialMappingManager.Instance.DrawVisualMeshes=" + SpatialMappingManager.Instance.DrawVisualMeshes);
        }

        private static void ToggleProcessedMesh()
        {
            SpatialUnderstanding.Instance.UnderstandingCustomMesh.DrawProcessedMesh = !SpatialUnderstanding.Instance.UnderstandingCustomMesh.DrawProcessedMesh;
            Debug.Log("SpatialUnderstanding -> SpatialUnderstanding.Instance.UnderstandingCustomMesh.DrawProcessedMesh=" + SpatialUnderstanding.Instance.UnderstandingCustomMesh.DrawProcessedMesh);
        }

        private void Update()
        {
            Update_DebugDisplay(Time.deltaTime);
            Update_KeyboardInput(Time.deltaTime);
        }

        public void OnSourceDetected(SourceStateEventData eventData)
        {
            // If the source has positional info and there is currently no visible source
            if (eventData.InputSource.SupportsInputInfo(eventData.SourceId, SupportedInputInfo.Position))
            {
                trackedHandsCount++;
            }
        }

        public void OnSourceLost(SourceStateEventData eventData)
        {
            if (eventData.InputSource.SupportsInputInfo(eventData.SourceId, SupportedInputInfo.Position))
            {
                trackedHandsCount--;
            }
        }

		bool reviewIsDone = false;
		bool colorPickerIsDone = false;


        public void OnInputClicked(InputClickedEventData eventData)
        {
            if ((SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.Scanning) &&
                !SpatialUnderstanding.Instance.ScanStatsReportStillWorking)
            {

				//Destroy (newMenuBit.transform);

				/*GameObject newMenuBit2 = Instantiate (PaintMateAreaDisplayPrefab, DebugDisplay.transform.position, DebugDisplay.transform.rotation);
				newMenuBit2.transform.SetParent (DebugDisplay.transform);
				newMenuBit2.transform.position = DebugDisplay.transform.position;

				newMenuBit2.GetComponentInChildren<TextMesh> ().text = "over 9000";
				*/

				SpatialUnderstanding.Instance.RequestFinishScan();

				//SpatialUnderstanding.Instance.GetComponentInChildren<Renderer> ().material.SetColor ("_WireColor", Color.red);
            }
				
			// Review phase, look at largrest wall, "reset if incorrect"
			// Click to Move onto 
			if (SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.Done
			   && !reviewIsDone) {

				// If clicked move Color Picker Mode

				// TODO: Toggle Mesh Off
				ToggleProcessedMesh();

				// TODO: Clear Lines
				SpaceVisualizer.Instance.ClearGeometry();

				// TODO: Draw Wall
				SpaceVisualizer.Instance.Query_Topology_PaintLargestWall ();


				reviewIsDone = true;

				StartCoroutine(LoadLevelAfterDelay(3)); 
			}

			// Palette Mode
//			if (SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.Done
//			   && reviewIsDone && !colorPickerIsDone) {
//
//
//
//
//
//			}



			// Paint-mate : If scan is done, press to place anchors for subtraction
			/*if ((SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.Done)
				// AND LESS THAN TWO ANCHOR POINTS
			) {
				// Set anchor
				// Raycast, to get point
				// Create GameObject at point
				var headPosition = Camera.main.transform.position;
				var gazeDirection = Camera.main.transform.forward;

				RaycastHit hitInfo;

				Debug.Log ("Raycast Attemp");

				if( Physics.Raycast(headPosition, gazeDirection, out hitInfo) ){
					//MeshRenderer.enabled = true;
					Debug.Log("Raycast success");

					Vector3 spawnPosition = hitInfo.point;

					GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
					cube.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
					cube.transform.position = spawnPosition;

				}

				//If there's two
			}*/

        }
    }
}