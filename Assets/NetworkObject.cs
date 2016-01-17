using UnityEngine;
using System.Collections;

// ネットワークオブジェクト抽象クラス
public abstract class NetworkObject : MonoBehaviour {

	public NetworkObjectData sharedNod; // 転送用オブジェクト
	public bool isLocal { get; set; }		 // ローカルかリモートか
	public float latestUpdate = 0f;			 // 最終更新時刻
	private float send_frequency_;			 // 送信頻度。１で１秒おき
	private static uint generation_ = 0; // ID 用の世代番号

	// コンストラクタ
	public NetworkObject()
	{
		isLocal = true;			// デフォルトはローカル
	}

	// 送信開始
	public void startSend(float freq)
	{
		Debug.Assert(isLocal);	// 送るのはローカルだけ
		send_frequency_ = freq;
		// 共有データを生成
		sharedNod = NetworkObjectData.create(NetworkManager.peerId, generation_);
		++generation_;			// 世代を進める
		StartCoroutine(send());	// 送信開始
	}

	// 送信処理
	private IEnumerator send()
	{
		var go = GameObject.Find("NetworkManager"); // マネージャ検索
		if (go != null) {
			var network_manager = go.GetComponent<NetworkManager>();
			for (;;) {   // 無限ループ
				network_manager.send(sharedNod); // 送信
				yield return new WaitForSeconds(send_frequency_); // 送信頻度
			}
		}
	}

	// 受信したリモートデータを設定
	public void setRemoteData(NetworkObjectData nod)
	{
		sharedNod = nod;
	}

	// 出現時の処理
	public abstract void spawnRemote(NetworkObjectData nod);
}
