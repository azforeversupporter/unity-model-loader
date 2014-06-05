using System;
using UnityEngine;
using System.Collections.Generic;
using UnityModelLoader.ModelLoader;

namespace UnityModelLoader
{
	public class ModelMenu : MonoBehaviour
	{
		void OnGUI()
		{
			var boxHeigth = 50;
			GUI.Box(new Rect(10, 10, 100, boxHeigth), "");
			
			if (GUI.Button (new Rect(20, 20, 60, 10), "Grenade"))
			{
				var loader = new ModelLoader.ModelLoader();
				var objects = loader.Load (@"C:\Users\rober_000\Desktop\3dsLoader\3DS Texture Loader\grenade.3DS");
				
				Debug.Log (objects.Count);
			}
		}
	}
}

