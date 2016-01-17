using UnityEngine;
using System.Collections;

public abstract class Mammal {
	public abstract void func();
}

public class Monkey : Mammal {
	public override void func()
	{
		Debug.Log("I'm a Monkey!");
	}
}

public class Human : Mammal {
	public override void func()
	{
		Debug.Log("I'm a Human!");
	}
}


public class TestDerive : MonoBehaviour {

	IEnumerator Start()
	{
		var list = new Mammal[5];
		for (var i = 0; i < list.Length; ++i) {
			if (Random.Range(0, 2) == 0) {
				list[i] = new Monkey();
			} else {
				list[i] = new Human();
			}
		}
		yield return null;

		foreach (var mammal in list) {
			mammal.func();
		}
		yield return null;
	}
}
