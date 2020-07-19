//
// Unityちゃん用の三人称カメラ
// 
// 2013/06/07 N.Kobyasahi
//
using UnityEngine;
using System.Collections;
using System.IO;

namespace UnityChan
{
	public class ThirdPersonCamera2 : MonoBehaviour
	{
		public float smooth = 3f;		// カメラモーションのスムーズ化用変数
		Transform standardPos;			// the usual position for the camera, specified by a transform in the game
		Transform frontPos;			// Front Camera locater
		Transform jumpPos;			// Jump Camera locater
        Transform fpsPos;
        private GameObject cameraObject;    // メインカメラへの参照
        private GameObject predictorObject;

        // スムーズに繋がない時（クイック切り替え）用のブーリアンフラグ
        bool bQuickSwitch = false;	//Change Camera Position Quickly
	
	
		void Start ()
		{
			// 各参照の初期化
			standardPos = GameObject.Find ("CamPos").transform;
		
			if (GameObject.Find ("FrontPos"))
				frontPos = GameObject.Find ("FrontPos").transform;

			if (GameObject.Find ("JumpPos"))
				jumpPos = GameObject.Find ("JumpPos").transform;

            if (GameObject.Find("FpsPos"))
                fpsPos = GameObject.Find("FpsPos").transform;

            if (GameObject.Find("Predictor")) {
                predictorObject = GameObject.Find("Predictor");
            } else {
                Debug.Log("Predictor not defined.");
            }

            //カメラをスタートする
            transform.position = standardPos.position;	
			transform.forward = standardPos.forward;

            //メインカメラを取得する
            cameraObject = GameObject.FindWithTag("MainCamera");
        }

        void FixedUpdate ()	// このカメラ切り替えはFixedUpdate()内でないと正常に動かない
		{
		
			if (Input.GetButton ("Fire1")) {	// left Ctlr	
                // Change Jump Camera
                // setCameraPositionJumpView ();
                setCameraPositionFpsView();
            } else if (Input.GetButton ("Fire2")) { //Alt	
				// Change Front Camera
				setCameraPositionFrontView ();
			} else {	
				// return the camera to standard position and direction
				setCameraPositionNormalView ();
			}

        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.Space)) {
                StartCoroutine("Capture");
            }

        }

        void setCameraPositionNormalView ()
		{
			if (bQuickSwitch == false) {
				// the camera to standard position and direction
				transform.position = Vector3.Lerp (transform.position, standardPos.position, Time.fixedDeltaTime * smooth);	
				transform.forward = Vector3.Lerp (transform.forward, standardPos.forward, Time.fixedDeltaTime * smooth);
			} else {
				// the camera to standard position and direction / Quick Change
				transform.position = standardPos.position;	
				transform.forward = standardPos.forward;
				bQuickSwitch = false;
			}
		}
	
		void setCameraPositionFrontView ()
		{
			// Change Front Camera
			bQuickSwitch = true;
			transform.position = frontPos.position;	
			transform.forward = frontPos.forward;
		}

		void setCameraPositionJumpView ()
		{
			// Change Jump Camera
			bQuickSwitch = false;
			transform.position = Vector3.Lerp (transform.position, jumpPos.position, Time.fixedDeltaTime * smooth);	
			transform.forward = Vector3.Lerp (transform.forward, jumpPos.forward, Time.fixedDeltaTime * smooth);		
		}

        void setCameraPositionFpsView() {
            // Change Jump Camera
            bQuickSwitch = false;
            transform.position = Vector3.Lerp(transform.position, fpsPos.position, Time.fixedDeltaTime * smooth);
            transform.forward = Vector3.Lerp(transform.forward, fpsPos.forward, Time.fixedDeltaTime * smooth);
        }

        [SerializeField] private static readonly string CAPUTURED_PICTURE_SAVE_DIRECTORY = "/";
        //[SerializeField] private Camera _captureCamera;

        public IEnumerator Capture() {
            var cam = cameraObject.GetComponent<Camera>();
            //Debug.Log("Screen Width : " + Screen.width);
            //Debug.Log("Screen  height: " + Screen.height);

            var coroutine = StartCoroutine(CaptureFromCamera(416, 416, cam));
            //var coroutine = StartCoroutine(CaptureFromCamera(Screen.width, Screen.height, cam));
            yield return coroutine;
        }

        private IEnumerator CaptureFromCamera(int width, int height, Camera camera) {
            var prevTexture = camera.targetTexture;

            // MainCamera の出力を2DTextureに出力して描画を待つ              
            //Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = new RenderTexture(width, height, 24);
            yield return new WaitForEndOfFrame();

            RenderTexture.active = camera.targetTexture;
            tex.ReadPixels(new Rect(0, 0, camera.targetTexture.width, camera.targetTexture.height), 0, 0);
            tex.Apply();

            // ファイル保存
            byte[] bytes = tex.EncodeToPNG();
            string savePath = Application.streamingAssetsPath + CAPUTURED_PICTURE_SAVE_DIRECTORY + "capture.png";
            File.WriteAllBytes(savePath, bytes);

            // 推論
            var yolo = predictorObject.GetComponent<TinyYolo>();
            yolo.doPredict(tex);

            Destroy(tex);

            if (camera.targetTexture != null) {
                camera.targetTexture.Release();
            }

            camera.targetTexture = prevTexture;


            yield break;
        }
    }
}