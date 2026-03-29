using Timberborn.ModManagerScene;
using UnityEngine;

public class OverhaulMiscellaneous : IModStarter {
	public void StartMod(IModEnvironment env) {
		Debug.Log(GetType().Name);
	}
}
