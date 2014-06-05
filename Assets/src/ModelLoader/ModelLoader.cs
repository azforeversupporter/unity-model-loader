using UnityEngine;
using System.Collections;
using UnityModelLoader.ModelLoader.Loaders;
using System.Collections.Generic;
using System.IO;

namespace UnityModelLoader.ModelLoader 
{
	public class ModelLoader 
	{
		public List<GameObject> Load(string path)
		{
			var loader = GetLoaderInstance (path);
			return loader.Load (path);
		}		
		
		private ILoader GetLoaderInstance(string path)
		{			
			var extension = Path.GetExtension (path);
			extension = extension.ToLowerInvariant();
			
			return loaderMapping[extension];
		}
				
		private ILoader loader;
		private Dictionary<string, ILoader> loaderMapping = new Dictionary<string, ILoader>
		{
			{ ".3ds", new DSLoader() }
		};
	}
}