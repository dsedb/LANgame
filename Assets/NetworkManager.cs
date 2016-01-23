using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

public class NetworkManager : MonoBehaviour {
	// リモートオブジェクト用のプレハブ
	public GameObject player_prefab_;
	public GameObject block_prefab_;

	private bool LOCAL_TEST = true; // テスト用
	private bool USE_RANDOM_ID = true; // ピアIDを乱数で作る

	// ブロードキャストアドレス設定
	// private const string broadcast_addr = "192.168.0.255";
	private const string broadcast_addr = "10.0.255.255";
	private const int PORT = 2002; // ポート

	// ポートを分けるのはデバッグ用。１台のPCで２つ立ち上げて通信テストする際にポート番号をずらす
	public int debug_send_port = PORT;
	public int debug_recv_port = PORT;

	public static uint peerId = 0; // ピアID
	private static NetworkManager singleton = null; // シングルトン
    private static bool sent_done = false;			// 送信用制御フラグ
	private static bool received_done = false;		// 受信用制御フラグ
	private static UdpClient send_udp; // 送信用UDPソケット
	private static UdpClient recv_udp; // 受信用UDPソケット

	private static Queue<byte> recv_fifo; // 受信用FIFO
	private static Queue<NetworkObjectData> send_fifo; // 送信用FIFO
	private Dictionary<ulong, GameObject> object_dict_; // ネットワークオブジェクト管理
	
	// コンストラクタ
	NetworkManager()
	{
		// コンストラクタでピアIDを確定
		peerId = create_unique_id();
	}

	// 送信完了コールバック。別スレッドから呼ばれる
	static void send_callback(IAsyncResult ar)
    {
		send_udp.EndSend(ar);
        sent_done = true;
    }
	
	// 送信要求
	public void send(NetworkObjectData nod)
    {
		// 送信内容をキューイングして、後でまとめて送信する
		send_fifo.Enqueue(nod);
	}
	
	// 送信要求処理（コルーチン）
	IEnumerator send_loop()
	{
		for (;;) {
			int count = 0;
			for (;;) {			// キューに何かが入るまで何もしない
				count = send_fifo.Count;
				if (count > 0)
					break;
				yield return null;
			}
			// キューに入ってきた
			var stream = new System.IO.MemoryStream(); // メモリストリーム作成
			const int MAXNOD = 32;					   // 送信上限数
			for (int i = 0; i < count; ++i) {
				var nod = send_fifo.Dequeue(); // キューから取り出して
				if (count - i <= MAXNOD) {		// 送信溢れを防ぐために上限を設定、新しいものだけを処理する
					nod.serialize(stream);		// シリアライズ
				}
			}
			byte[] sending_data = stream.GetBuffer(); /* シリアライズ結果を取得。
														 data.Length は実装依存なので信用しない */
			int nod_size = NetworkObjectData.getSize();
			int datasize = nod_size * count; // 総サイズを算出
			int maxsize = nod_size * 20; // パケットあたりの最大値。パケットロストを考慮してNOD単位にする
			byte[] data = new byte[maxsize];   // 送信バッファ
			int index = 0;
			// パケット最大数で分割して何度かに分けて送る
			while (index < datasize) {
				int size = datasize - index; // 一度に送る量
				if (size > maxsize) {		  // 最大値を超えていたら
					size = maxsize;			  // 最大値にする
				}
				System.Buffer.BlockCopy(sending_data, index, data, 0, size); // バッファにコピー
				index += size;	// インデクス移動

				sent_done = false;
				send_udp.BeginSend(data, size, new AsyncCallback(send_callback), null); // ノンブロック送信
				while (!sent_done) {		// コールバックが呼ばれるまで待つ
					yield return null;
				}
			}
			yield return new WaitForSeconds(0.1f); // 効率のため間隔を空ける
		}
    }

	// 受信コールバック。別スレッドから呼ばれる
	static void receive_callback(IAsyncResult ar)
	{
		IPEndPoint end_point = null;
        var data = recv_udp.EndReceive(ar, ref end_point); // 受信データ取得
		lock(((ICollection)recv_fifo).SyncRoot) { // ロック
			foreach (var d in data) {
				recv_fifo.Enqueue(d); // 受信用キューに詰める
			}
		}
		received_done = true;		// 受信完了
    }

	// 受信処理（コルーチン）
    IEnumerator receive_loop()
	{
        for (;;) {
			received_done = false;
            recv_udp.BeginReceive(new AsyncCallback(receive_callback), null); // 受信開始
			while (!received_done) {	// 受信完了したら抜ける
				yield return null;
			}

			var nod_size = NetworkObjectData.getSize(); // 固定サイズ取得
			var nod_dict = new Dictionary<ulong, NetworkObjectData>();
			lock(((ICollection)recv_fifo).SyncRoot) {
				while (recv_fifo.Count > nod_size) {
					var nod = NetworkObjectData.create(recv_fifo);
					if (nod == null)		  // 不正なデータは
						continue;			  // 処理しない
					if (nod.peerId == peerId) // 自分の場合は
						continue;			  // 処理しない
					nod_dict[nod.getGlobalId()] = nod; /* オブジェクトに登録。
														  重なっていた分は上書きして最新のみ有効に */
				}
				foreach (var nod in nod_dict.Values) { // 更新されたものを列挙
					singleton.OnReceiveEvent(nod);	   // 更新処理実行
				}
			}

			yield return null;
        }
    }

	// ピア用のユニークIDを作成
	uint create_unique_id()
	{
		uint id = 0;
		if (USE_RANDOM_ID) {
			var rand = new System.Random();
			id = (uint)rand.Next();
		} else {
			var nicList = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
			foreach (var nic in nicList) {
				var ipInfo = nic.GetIPProperties();
				foreach (var addr in ipInfo.UnicastAddresses) {
					var bytes = addr.Address.GetAddressBytes();
					// RFC1918
					if (bytes[0] == 10 ||
						(bytes[0] == 172 && 16 <= bytes[1] && bytes[1] <= 31) ||
						(bytes[0] == 192 && bytes[1] == 168)) {
						id = System.BitConverter.ToUInt32(bytes, 0);
						break;
					}
				}
			}
		}
		return id;
	}

	void Awake()
	{
		Debug.Assert(singleton == null);		// 複数作られないように
		singleton = this;
		Debug.Log(string.Format("peerId:{0:X08}", peerId));
		Debug.Assert(peerId != 0);

		send_fifo = new Queue<NetworkObjectData>();  // 送信用FIFO
		recv_fifo = new Queue<byte>();	 // 受信用FIFO

        send_udp = new UdpClient(); // 送信UDP
        send_udp.EnableBroadcast = true;
        send_udp.Connect(broadcast_addr,
						 LOCAL_TEST ? debug_send_port : PORT); // 接続
		var end_point = new IPEndPoint(IPAddress.Any, LOCAL_TEST ? debug_recv_port : PORT);
		recv_udp = new UdpClient(end_point); // 受信UDP
		recv_udp.EnableBroadcast = true;

        StartCoroutine(send_loop()); // 送信処理開始
        StartCoroutine(receive_loop()); // 受信処理開始

		object_dict_ = new Dictionary<ulong, GameObject>(); // リモートオブジェクト辞書
	}

	void OnDestroy()
	{
		// 念のため閉じておく
		send_udp.Close();
		recv_udp.Close();
	}

	void Update()
	{
		sweepObject();
	}
	
	// データ受信
	void OnReceiveEvent(NetworkObjectData nod)
	{
		ulong id = nod.getGlobalId();
		GameObject go = null;
		if (object_dict_.ContainsKey(id)) { // 過去に出現したことがある
			// 情報更新のみ
			go = object_dict_[id];
			var nobj = go.GetComponent<NetworkObject>();
			nobj.setRemoteData(nod);
		} else {				// 初めて登場する
			// 生成
			GameObject prefab = null;
			string name = "";
			switch (nod.type) {
				case NetworkObjectData.Type.Player:
					prefab = player_prefab_;
					name = "player_";
					break;
				case NetworkObjectData.Type.Block:
					prefab = block_prefab_;
					name = "block_";
					break;
			}
			if (prefab != null) {
				go = Instantiate(prefab, nod.position, nod.rotation) as GameObject;
				go.name = name + nod.getName();
				var nobj = go.GetComponent<NetworkObject>();
				nobj.isLocal = false;
				object_dict_[id] = go;
				nobj.spawnRemote(nod);
			}
		}

		if (go != null) {
			var nobj = go.GetComponent<NetworkObject>();
			nobj.latestUpdate = Time.time;
		}
	}
	
	// 切断を監視して掃除
	void sweepObject()
	{
		var del_list = new List<ulong>();
		foreach (KeyValuePair<ulong, GameObject> pair in object_dict_) {
			var nobj = pair.Value.GetComponent<NetworkObject>();
			if (Time.time - nobj.latestUpdate > 10f) { // 最終更新から１０秒経ったら
				Destroy(pair.Value);				   // 切断とみなして削除
				del_list.Add(pair.Key);
			}
		}
		foreach (var del in del_list) {
			object_dict_.Remove(del); // 登録からも削除
		}
	}
}
