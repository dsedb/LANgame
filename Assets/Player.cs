using UnityEngine;
using System.Collections;

public class Player : NetworkObject {

	public string name_ = "--";
	public GameObject block_prefab_; // 生成用
	public Color color_ = Color.white;
	private float jumping_time_; // ジャンプアクション用
	private int creatable_block_num_ = 16; // 生成可能ブロック数

	// リモートオブジェクト生成
	public override void spawnRemote(NetworkObjectData nod)
	{
		// transform は Instantiate で更新済み
		name_ = nod.getName();	// 名前更新
		color_ = nod.color;		// 色更新
		Destroy(GetComponent<Collider>()); // リモートオブジェクトはコリジョンなしにする
	}
	
	void Start()
	{
		if (isLocal) {			// ローカル
			startSend(0.1f /* freq */);		// 送信開始
			sharedNod.type = NetworkObjectData.Type.Player; // タイプ設定
			sharedNod.update(transform); // 位置・角度情報更新
			sharedNod.color = (Color32)color_;				// 色更新
			sharedNod.setName(name_);						// 名前更新
			jumping_time_ = Time.time;						// ジャンプ制御
		}
		// 色反映
		GetComponent<MeshRenderer>().material.SetColor("_Color", color_);
	}

	void Update() 
	{
		if (isLocal) {			// ローカル
			var controller = GetComponent<CharacterController>();
			if (controller.isGrounded) {		   // 接地している
				if (Input.GetButtonDown("Jump")) { // Spaceキー
					jumping_time_ = Time.time;	   // ジャンプ開始
				}
			}
			// 入力から速度を生成
			var velocity = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
			velocity *= 6f /* speed */;
			if (Time.time - jumping_time_ < 0.25f /* jump duration */ && Input.GetButton("Jump")) {
				// 飛ぶ
				velocity.y = 20f /* jump speed */;
			} else {
				// 落下する
				velocity.y -= 20f /* gravity speed */;
			}
			if (Input.GetButtonDown("Fire1")) { // 左Ctrlキー
				if (creatable_block_num_ > 0) {
					// ブロック生成
					var position = transform.TransformPoint(Vector3.forward*2);
					var go = Instantiate(block_prefab_, position, Quaternion.identity) as GameObject;
					go.name = "block_" + name_; // 名前設定
					Block block = go.GetComponent<Block>();
					block.setName(name_);
					block.setColor(color_);
					--creatable_block_num_;
				}
			}
			// キャラクターコントローラで動かす
			controller.Move(velocity * Time.deltaTime);
			// 移動方向に回転させる
			var h_velocity = new Vector3(velocity.x, 0f, velocity.z);
			if (h_velocity.sqrMagnitude > 0f) {
				transform.rotation = Quaternion.LookRotation(h_velocity);
			}
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
