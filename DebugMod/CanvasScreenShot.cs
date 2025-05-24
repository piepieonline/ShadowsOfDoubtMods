using Cpp2IL.Core.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

using UnhollowerBaseLib;
using UniverseLib;

namespace DebugMod
{
    static class EncodeAsPngExt
    {
        private delegate IntPtr EncodeAsPngDelegate(IntPtr texturePtr);
        private static readonly EncodeAsPngDelegate ourEncodeAsPng = IL2CPP.ResolveICall<EncodeAsPngDelegate>("UnityEngine.ImageConversion::EncodeToPNG");
        public static Il2CppStructArray<byte>? OurEncodeAsPng(this Texture2D texture)
        {
            var texturePointer = (System.IntPtr)typeof(Texture2D).GetMethod("get_Pointer").Invoke(texture, null);
            DebugModPlugin.PluginLogger.LogInfo($"Texture pointer: {texturePointer}");
            var arrayPtr = ourEncodeAsPng.Invoke(texturePointer);
            DebugModPlugin.PluginLogger.LogInfo($"Data pointer: {arrayPtr}");
            if (arrayPtr == IntPtr.Zero) return null;
            return new Il2CppStructArray<byte>(arrayPtr);
        }
    }

    public class CanvasScreenShot : MonoBehaviour
    {
        /*
     CanvasScreenShot by programmer.
     http://stackoverflow.com/questions/36555521/unity3d-build-png-from-panel-of-a-unity-ui#36555521
     http://stackoverflow.com/users/3785314/programmer
     */

        //Events
        public delegate void takePictureHandler(byte[] pngArray);
        public static event takePictureHandler OnPictureTaken;

        private GameObject duplicatedTargetUI;

        //Store all other canvas that will be disabled and re-anabled after screenShot
        private Canvas[] allOtherCanvas;

        //takes Screenshot
        public void takeScreenShot(Canvas canvasPanel, bool createNewInstance = true)
        {
            DebugModPlugin.PluginLogger.LogInfo($"Prestarting");
            UniverseLib.RuntimeHelper.StartCoroutine(_takeScreenShot(canvasPanel, createNewInstance));
        }

        public void SaveTestScreenshot()
        {
            // Create a texture the size of the screen, RGB24 format
            int width = Screen.width;
            int height = Screen.height;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Read screen contents into the texture
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            TestSaveTexture(tex);
        }

        public void TestSaveTexture(Texture2D tex)
        {
            DebugModPlugin.PluginLogger.LogInfo($"Saving screenshot");
            string path = "D:\\CanvasScreenShot.png";
            System.IO.File.WriteAllBytes(path, tex.OurEncodeAsPng());
            DebugModPlugin.PluginLogger.LogInfo($"Screenshot complete");
        }

        public void TakeScreenshotOfCaseboard()
        {
            Canvas canvas = GameObject.Find("CaseCanvas").GetComponent<Canvas>();

            if(!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
            }

            var zoom = canvas.GetComponentInChildren<ZoomContent>();
            zoom.ResetPivot();
            zoom.ApplyZoom(0);

            var newBG = new GameObject("SSBackground");
            newBG.transform.parent = GameObject.Find("CaseCanvas/CorkBoard/Viewport/ContentContainer").transform;
            var newBGImage = newBG.AddComponent<Image>();
            newBGImage.sprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, Texture2D.blackTexture.width, Texture2D.blackTexture.height), new Vector2(0.5f, 0.5f), 100f);

            takeScreenShot(canvas, false);
        }

        private System.Collections.IEnumerator _takeScreenShot(Canvas canvasPanel, bool createNewInstance = true)
        {
            DebugModPlugin.PluginLogger.LogInfo($"Starting screenshot");

            //Get Visible Canvas In the Scene
            allOtherCanvas = getAllCanvasInScene(false);

            //Hide all the other Visible Canvas except the one that is passed in as parameter(Canvas we want to take Picture of)
            showCanvasExcept(allOtherCanvas, canvasPanel, false);
            //Reset the position so that both UI will be in the-same place if we make the duplicate a child
            resetPosAndRot(gameObject);

            //Check if we should operate on the original image or make a duplicate of it
            if (createNewInstance)
            {
                //Duplicate the Canvas we want to take Picture of
                duplicatedTargetUI = duplicateUI(canvasPanel.gameObject, "ScreenShotUI");
                //Make this game object the parent of the Canvas
                duplicatedTargetUI.transform.SetParent(gameObject.transform);

                //Hide the orginal Canvas we want to take Picture of
                showCanvas(canvasPanel, false);
            }
            else
            {
                //No duplicate. Use original GameObject
                //Make this game object the parent of the Canvas
                canvasPanel.transform.SetParent(gameObject.transform);
            }

            RenderMode defaultRenderMode;

            //Change the duplicated Canvas to RenderMode to overlay
            Canvas duplicatedCanvas = null;
            if (createNewInstance)
            {
                duplicatedCanvas = duplicatedTargetUI.GetComponent<Canvas>();
                defaultRenderMode = duplicatedCanvas.renderMode;
                duplicatedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            else
            {
                defaultRenderMode = canvasPanel.renderMode;
                canvasPanel.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            //////////////////////////////////////Finally Take ScreenShot///////////////////////////////
            yield return new WaitForEndOfFrame();
            Texture2D screenImage = new Texture2D(Screen.width, Screen.height);
            //Get Image from screen
            screenImage.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenImage.Apply();

            //Convert to png
            byte[] pngBytes = screenImage.OurEncodeAsPng();

            // FOR TESTING/DEBUGGING PURPOSES ONLY. COMMENT THIS
            DebugModPlugin.PluginLogger.LogInfo($"Saving screenshot");
            string path = "D:\\CanvasScreenShot.png";
            System.IO.File.WriteAllBytes(path, pngBytes);

            //Notify functions that are subscribed to this event that picture is taken then pass in image bytes as png
            if (OnPictureTaken != null)
            {
                OnPictureTaken(pngBytes);
            }


            ///////////////////////////////////RE-ENABLE OBJECTS

            //Change the duplicated Canvas RenderMode back to default Value
            if (createNewInstance)
            {
                duplicatedCanvas.renderMode = defaultRenderMode;
            }
            else
            {
                canvasPanel.renderMode = defaultRenderMode;
            }
            //Un-Hide all the other Visible Canvas except the one that is passed in as parameter(Canvas we want to take Picture of)
            showCanvas(allOtherCanvas, true);

            //Un-hide the orginal Canvas we want to take Picture of
            showCanvas(canvasPanel, true);

            if (createNewInstance)
            {
                //Destroy the duplicated GameObject
                Destroy(duplicatedTargetUI, 1f);
            }
            else
            {
                //Remove the Canvas as parent 
                canvasPanel.transform.SetParent(null);
            }
        }

        private GameObject duplicateUI(GameObject parentUICanvasOrPanel, string newOBjectName)
        {
            GameObject tempObj = Instantiate(parentUICanvasOrPanel);
            tempObj.name = newOBjectName;
            return tempObj;
        }


        private Image[] getAllImagesFromCanvas(GameObject canvasParentGameObject, bool findDisabledCanvas = false)
        {
            Image[] tempImg = canvasParentGameObject.GetComponentsInChildren<Image>(findDisabledCanvas);
            if (findDisabledCanvas)
            {
                return tempImg;
            }
            else
            {
                System.Collections.Generic.List<Image> canvasList = new System.Collections.Generic.List<Image>();
                for (int i = 0; i < tempImg.Length; i++)
                {
                    if (tempImg[i].enabled)
                    {
                        canvasList.Add(tempImg[i]);
                    }
                }
                return canvasList.ToArray();
            }
        }

        private Text[] getAllTextsFromCanvas(GameObject canvasParentGameObject, bool findDisabledCanvas = false)
        {
            Text[] tempImg = canvasParentGameObject.GetComponentsInChildren<Text>(findDisabledCanvas);
            if (findDisabledCanvas)
            {
                return tempImg;
            }
            else
            {
                System.Collections.Generic.List<Text> canvasList = new System.Collections.Generic.List<Text>();
                for (int i = 0; i < tempImg.Length; i++)
                {
                    if (tempImg[i].enabled)
                    {
                        canvasList.Add(tempImg[i]);
                    }
                }
                return canvasList.ToArray();
            }
        }

        private Canvas[] getAllCanvasFromCanvas(Canvas canvasParentGameObject, bool findDisabledCanvas = false)
        {
            Canvas[] tempImg = canvasParentGameObject.GetComponentsInChildren<Canvas>(findDisabledCanvas);
            if (findDisabledCanvas)
            {
                return tempImg;
            }
            else
            {
                System.Collections.Generic.List<Canvas> canvasList = new System.Collections.Generic.List<Canvas>();
                for (int i = 0; i < tempImg.Length; i++)
                {
                    if (tempImg[i].enabled)
                    {
                        canvasList.Add(tempImg[i]);
                    }
                }
                return canvasList.ToArray();
            }
        }

        //Find Canvas.
        private Canvas[] getAllCanvasInScene(bool findDisabledCanvas = false)
        {
            // Canvas[] tempCanvas = GameObject.FindObjectsOfType<Canvas>();
            Canvas[] tempCanvas = RuntimeHelper.FindObjectsOfTypeAll<Canvas>();
            if (findDisabledCanvas)
            {
                return tempCanvas;
            }
            else
            {
                System.Collections.Generic.List<Canvas> canvasList = new System.Collections.Generic.List<Canvas>();
                for (int i = 0; i < tempCanvas.Length; i++)
                {
                    if (tempCanvas[i].enabled)
                    {
                        canvasList.Add(tempCanvas[i]);
                    }
                }
                return canvasList.ToArray();
            }
        }

        //Disable/Enable Images
        private void showImages(Image[] imagesToDisable, bool enableImage = true)
        {
            for (int i = 0; i < imagesToDisable.Length; i++)
            {
                imagesToDisable[i].enabled = enableImage;
            }
        }

        //Disable/Enable Texts
        private void showTexts(Text[] imagesToDisable, bool enableTexts = true)
        {
            for (int i = 0; i < imagesToDisable.Length; i++)
            {
                imagesToDisable[i].enabled = enableTexts;
            }
        }


        //Disable/Enable Canvas
        private void showCanvas(Canvas[] canvasToDisable, bool enableCanvas = true)
        {
            for (int i = 0; i < canvasToDisable.Length; i++)
            {
                canvasToDisable[i].enabled = enableCanvas;
            }
        }


        //Disable/Enable one canvas
        private void showCanvas(Canvas canvasToDisable, bool enableCanvas = true)
        {
            canvasToDisable.enabled = enableCanvas;
        }

        //Disable/Enable Canvas Except
        private void showCanvasExcept(Canvas[] canvasToDisable, Canvas ignoreCanvas, bool enableCanvas = true)
        {
            for (int i = 0; i < canvasToDisable.Length; i++)
            {
                if (!(canvasToDisable[i] == ignoreCanvas))
                {
                    canvasToDisable[i].enabled = enableCanvas;
                }
            }
        }

        //Disable/Enable Canvas Except
        private void showCanvasExcept(Canvas[] canvasToDisable, Canvas[] ignoreCanvas, bool enableCanvas = true)
        {
            for (int i = 0; i < canvasToDisable.Length; i++)
            {
                for (int j = 0; j < ignoreCanvas.Length; j++)
                {
                    if (!(canvasToDisable[i] == ignoreCanvas[j]))
                    {
                        canvasToDisable[i].enabled = enableCanvas;
                    }
                }
            }
        }

        //Reset Position
        private void resetPosAndRot(GameObject posToReset)
        {
            posToReset.transform.position = Vector3.zero;
            posToReset.transform.rotation = Quaternion.Euler(Vector3.zero);
        }

    }

    public enum SCREENSHOT_TYPE
    {
        IMAGE_AND_TEXT, IMAGE_ONLY, TEXT_ONLY
    }
}