using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// See _ReadMe.txt for an overview
[ExecuteInEditMode]
public class CommandBufferBlurRefraction : MonoBehaviour
{
	public Shader m_BlurShader;
	private Material m_Material;

	private Camera m_Cam;

	public float blurPower = 1.0f;

	// We'll want to add a command buffer on any camera that renders us,
	// so have a dictionary of them.
	private Dictionary<Camera,CommandBuffer> m_Cameras = new Dictionary<Camera,CommandBuffer>();

	// Remove command buffers from all cameras we added into
	private void Cleanup()
	{
		foreach (var cam in m_Cameras)
		{
			if (cam.Key)
			{
				cam.Key.RemoveCommandBuffer (CameraEvent.AfterSkybox, cam.Value);
			}
		}
		m_Cameras.Clear();
		Object.DestroyImmediate (m_Material);
	}

	public void OnEnable()
	{
		Cleanup();
	}

	public void OnDisable()
	{
		Cleanup();
	}

	// Whenever any camera will render us, add a command buffer to do the work on it
	public void OnWillRenderObject()
	{
		var act = gameObject.activeInHierarchy && enabled;
		if (!act)
		{
			Cleanup();
			return;
		}
		
		var cam = Camera.current;
		if (!cam)
			return;

		CommandBuffer buf = null;
		// Did we already add the command buffer on this camera? Nothing to do then.
		if (m_Cameras.ContainsKey(cam))
			return;

		if (!m_Material)
		{
			m_Material = new Material(m_BlurShader);
			m_Material.hideFlags = HideFlags.HideAndDontSave;
		}

		buf = new CommandBuffer();
		buf.name = "Grab screen and blur";
		m_Cameras[cam] = buf;

		// copy screen into temporary RT
		int screenCopyID = Shader.PropertyToID("_ScreenCopyTexture");
		buf.GetTemporaryRT (screenCopyID, -1, -1, 0, FilterMode.Bilinear);
		buf.Blit (BuiltinRenderTextureType.CurrentActive, screenCopyID);
		
		// get two smaller RTs
		int blurredID1 = Shader.PropertyToID("_Temp1");
		int blurredID2 = Shader.PropertyToID("_Temp2");
		int blurredID3 = Shader.PropertyToID("_Temp3");
		buf.GetTemporaryRT (blurredID1, -2, -2, 0, FilterMode.Bilinear);
		buf.GetTemporaryRT (blurredID2, -2, -2, 0, FilterMode.Bilinear);
		buf.GetTemporaryRT (blurredID3, -2, -2, 0, FilterMode.Bilinear);

		// downsample screen copy into smaller RT, release screen RT
		buf.Blit (screenCopyID, blurredID1);
		buf.ReleaseTemporaryRT (screenCopyID); 
		buf.Blit (blurredID1, blurredID2);

		const int blurCount1 = 2;
		for (var i=0; i<blurCount1; i++) {
			var fOffset = (float)(1 << (i + 1));
			// horizontal blur
			buf.SetGlobalVector("offsets", new Vector4(fOffset/Screen.width,0,0,0));
			buf.Blit (blurredID1, blurredID3, m_Material);
			// vertical blur
			buf.SetGlobalVector("offsets", new Vector4(0,fOffset/Screen.height,0,0));
			buf.Blit (blurredID3, blurredID1, m_Material);
		}
		
		const int blurCount2 = 4;
		for (var i=0; i<blurCount2; i++) {
			var fOffset = (float)(1 << (i + 1));
			// horizontal blur
			buf.SetGlobalVector("offsets", new Vector4(fOffset/Screen.width,0,0,0));
			buf.Blit (blurredID2, blurredID3, m_Material);
			// vertical blur
			buf.SetGlobalVector("offsets", new Vector4(0,fOffset/Screen.height,0,0));
			buf.Blit (blurredID3, blurredID2, m_Material);
		}

		//Debug.Log(blurPower.ToString());
		if (Application.isPlaying) 
			gameObject.GetComponent<MeshRenderer>().material.SetFloat("_BlurPower", blurPower);
		else Shader.SetGlobalFloat("_BlurPower", blurPower);
		buf.SetGlobalTexture("_GrabBlurTexture1", blurredID1);
		buf.SetGlobalTexture("_GrabBlurTexture2", blurredID2);

		cam.AddCommandBuffer (CameraEvent.AfterSkybox, buf);
	}	
}
