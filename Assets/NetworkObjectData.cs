using UnityEngine;
using System.Collections.Generic;

public class NetworkObjectData {
	private static int cached_data_size = 0; // シリアライズのサイズは固定なので一度計算したらそれを使い回す
	public enum Type {
		Player,
		Block,
	}
	private const uint MAGIC = 0x5449414b; // 整合性を確認するマジックナンバー
	private const int VERSION = 100;	   // バージョン
	private const int DATA_SIZE = 8;	   // 汎用データ領域のサイズ

	// シリアライズデータここから
	private uint peer_id_;
	private uint object_id_;
	private Type type_;
	private Color32 color_;
	private Vector3 position_;
	private Quaternion rotation_;
	private byte[] data_;		// 名前などに使用する多目的データ。サイズ固定
	// シリアライズデータここまで

	// アクセサ群
	public uint peerId { get { return peer_id_; } }
	public uint objectId { get { return object_id_; } }
	public Type type { get { return type_; } set { type_ = value; } }
	public Vector3 position { get { return position_; } set { position_ = value; } }
	public Quaternion rotation { get { return rotation_; } set { rotation_ = value; } }
	public Color32 color { get { return color_; } set { color_ = value; } }

	// 多目的データを利用した名前
	public string getName()
	{
		return System.Text.Encoding.ASCII.GetString(data_).TrimEnd('\0');
	}
	public void setName(string name)
	{
		byte[] data = System.Text.Encoding.ASCII.GetBytes(name);
		for (var i = 0; i < data_.Length; ++i) {
			if (i < data.Length)
				data_[i] = data[i];
			else
				data_[i] = 0;
		}
	}
	
	// 直接的な生成を禁止
	private NetworkObjectData() {}

	// ローカル（送信元）オブジェクト用の作成
	public static NetworkObjectData create(uint peer_id, uint object_id)
	{
		var nod = new NetworkObjectData();
		nod.peer_id_ = peer_id;
		nod.object_id_ = object_id;
		nod.data_ = new byte[DATA_SIZE];
		for (var i = 0; i < DATA_SIZE; ++i)
			nod.data_[i] = 0;
		return nod;
	}
	public static NetworkObjectData create()
	{
		return create(0, 0);
	}

	// リモート（受診先）オブジェクト用の作成、デシリアライズで作成
	public static NetworkObjectData create(Queue<byte> fifo)
	{
		var nod = NetworkObjectData.create();
		bool success = nod.deserialize(fifo);
		if (success)
			return nod;
		else
			return null;
	}

	// シリアライズに必要なバイト数を取得。動的に計算され、キャッシュされる
	public static int getSize()
	{
		if (cached_data_size == 0) {
			var nod = create();
			var fifo = new Queue<byte>();
			nod.serialize(fifo);
			cached_data_size = fifo.Count;
		}
		return cached_data_size;
	}

	// 送信用：位置・回転の更新
	public void update(Transform transform)
	{
		position_ = transform.position;
		rotation_ = transform.rotation;
	}

	// ピアとオブジェクトのIDを合成してワールドユニークなIDを生成する
	public ulong getGlobalId()
	{
		return (ulong)peer_id_ << 32 | (ulong)object_id_;
	}

	// シリアライズ
	public void serialize(Queue<byte> fifo)
	{
		byte[] data;
		int size;
		serialize(out data, out size);
		for (var i = 0; i < size; ++i) {
			fifo.Enqueue(data[i]);
		}
	}
	public void serialize(out byte[] buffer, out int size)
	{
		var stream = new System.IO.MemoryStream();
		serialize(stream);
		buffer = stream.GetBuffer();
		// buffer は大きめに確保されているので buffer.Length はサイズにならない
		size = (int)stream.Seek(0, System.IO.SeekOrigin.Current); // よって seek を使う
		stream.Seek(0, System.IO.SeekOrigin.Begin);
	}
	// シリアライズ本体
	public void serialize(System.IO.MemoryStream stream)
	{
		var writer = new System.IO.BinaryWriter(stream);
		writer.Write(MAGIC);
		writer.Write(VERSION);
		writer.Write(peer_id_);
		writer.Write(object_id_);
		writer.Write((byte)type_);
		writer.Write(color_.r);
		writer.Write(color_.g);
		writer.Write(color_.b);
		writer.Write(position_.x);
		writer.Write(position_.y);
		writer.Write(position_.z);
		var r = rotation_.eulerAngles; // オイラー角に変換
		writer.Write(r.x);
		writer.Write(r.y);
		writer.Write(r.z);
		writer.Write(data_, 0 /* index */, data_.Length);
	}


	// デシリアライズ用変換ユーティリティ
	byte readByte(Queue<byte> fifo)
	{
		return fifo.Dequeue();
	}
	byte[] readBytes(Queue<byte> fifo, int size)
	{
		var data = new byte[size];
		for (var i = 0; i < size; ++i) {
			data[i] = fifo.Dequeue();
		}
		return data;
	}
	uint readUInt32(Queue<byte> fifo)
	{
		var data = readBytes(fifo, 4);
		return System.BitConverter.ToUInt32(data, 0 /* startIndex */);
	}
	float readSingle(Queue<byte> fifo)
	{
		var data = readBytes(fifo, 4);
		return System.BitConverter.ToSingle(data, 0 /* startIndex */);
	}


	// デシリアライズ本体	return: 成否
	public bool deserialize(Queue<byte> fifo)
	{
		uint magic = readUInt32(fifo);
		if (magic != MAGIC) {
			Debug.LogWarning("data corrupted.");
			return false;
		}
		uint version = readUInt32(fifo);
		if (version > VERSION) {
			Debug.LogWarning("detect future version.");
			return false;
		}
		peer_id_ = readUInt32(fifo);
		object_id_ = readUInt32(fifo);
		type_ = (NetworkObjectData.Type)readByte(fifo);
		byte r = readByte(fifo);
		byte g = readByte(fifo);
		byte b = readByte(fifo);
		color_ = new Color32(r, g, b, 255 /* alpha */);
		position_.x = readSingle(fifo);
		position_.y = readSingle(fifo);
		position_.z = readSingle(fifo);
		var rot = new Vector3();
		rot.x = readSingle(fifo);
		rot.y = readSingle(fifo);
		rot.z = readSingle(fifo);
		rotation_.eulerAngles = rot; // オイラー角から復元
		data_ = readBytes(fifo, data_.Length);
		return true;
	}
}
