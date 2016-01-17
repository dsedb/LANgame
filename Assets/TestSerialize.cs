using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;

// シリアライズテスト
public class TestSerialize : MonoBehaviour {

	private void write(string path)
	{
		// 生成
		var nod = NetworkObjectData.create(111 /* peerId */, 128 /* objectId */);
		// 値を適当に入れる
		nod.type = NetworkObjectData.Type.Player;
		nod.setName("hoge");
		nod.position = Vector3.one;
		
		// fifo 作成
		var fifo = new Queue<byte>();
		// シリアライズ
		nod.serialize(fifo);
		
		// ファイルに書き込み
		using (var write_stream = new FileStream(path, FileMode.Create, FileAccess.Write)) {
			using (var writer = new BinaryWriter(write_stream)) {
				foreach (var b in fifo) {
					writer.Write(b); // １バイトずつ書き込む
				}
			}
		}
	}

	private void read(string path)
	{
		// fifo 作成
		var fifo = new Queue<byte>();
		
		// ファイルから読み込み
		using (var read_stream = new FileStream(path, FileMode.Open, FileAccess.Read)) {
			using (var reader = new BinaryReader(read_stream)) {
				for (;;) {
					var chunk = reader.ReadBytes(1); // １バイトずつ読んで
					if (chunk.Length <= 0)			 // 読めなかったら終了
						break;
					fifo.Enqueue(chunk[0]); // キューに入れる
				}
			}
		}
		
		bool success = true;
		// オブジェクトをでシリアライズで作成
		var nod2 = NetworkObjectData.create(fifo);

		// 結果を検証
		if (nod2.peerId != 111) {
			Debug.LogError("peerId error!");
			success = false;
		}
		if (nod2.objectId != 128) {
			Debug.LogError("objectId error!");
			success = false;
		}
		if (nod2.type != NetworkObjectData.Type.Player) {
			Debug.LogError("type error!");
			success = false;
		}
		if (nod2.position != Vector3.one) {
			Debug.LogError("position error!");
			success = false;
		}
		if (nod2.getName() != "hoge") {
			Debug.LogError("name error!");
			success = false;
		}
		if (success) {
			Debug.Log("OK!");
		}
	}

	IEnumerator Start ()
	{
		string path = Application.dataPath + "/test.dat";
//		write(path);
//		yield return null;
		read(path);
		yield return null;
	}
}
