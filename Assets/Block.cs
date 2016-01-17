using UnityEngine;
using System.Collections;

public class Block : NetworkObject {

	private string name_;
	private Color color_;
	public void setName(string name) { name_ = name; }
	public void setColor(Color col) { color_ = col; } 

	// リモートオブジェクト生成
	public override void spawnRemote(NetworkObjectData nod)
	{
		// transform は Instantiate で更新済み
		name_ = nod.getName();	// 名前更新
		color_ = nod.color;					// 色更新
		Destroy(GetComponent<Rigidbody>()); // リモートオブジェクトは物理なしにする
	}

	void Start()
	{
		if (isLocal) {			// ローカル
			startSend(0.25f /* freq */);		// 送信開始
			sharedNod.type = NetworkObjectData.Type.Block; // タイプ設定
			sharedNod.update(transform); // 位置・角度情報更新
			sharedNod.color = (Color32)color_;			   // 色更新
			sharedNod.setName(name_);						// 名前更新
		}
		// 色反映
		GetComponent<MeshRenderer>().material.SetColor("_Color", color_);
	}
	
	void Update()
	{
		if (isLocal) {			// ローカル
			// 共有情報を更新
			sharedNod.update(transform);
			sharedNod.color = (Color32)color_;
			sharedNod.setName(name_);
		} else {				// リモート
			if (sharedNod != null) {
				name_ = sharedNod.getName();
				color_ = sharedNod.color;
				// 位置・角度は補間する
				transform.position = Vector3.Lerp(transform.position, sharedNod.position, 0.1f);
				transform.rotation = Quaternion.Lerp(transform.rotation, sharedNod.rotation, 0.1f);
			}
		}
		// 色反映
		GetComponent<MeshRenderer>().material.SetColor("_Color", color_);
	}
}
