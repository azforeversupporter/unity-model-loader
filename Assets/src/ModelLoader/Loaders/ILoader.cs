using System;
using UnityEngine;
using System.Collections.Generic;

namespace UnityModelLoader.ModelLoader.Loaders
{
	public interface ILoader
	{
		List<GameObject> Load(string path);
		int ObjectCount { get; }
	}
}