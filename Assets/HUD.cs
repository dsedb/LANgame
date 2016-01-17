using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class HUD : MonoBehaviour {

	public GameObject namePrefab_; // 名前用のプレハブ
	private Transform canvas_;	   // 追加先のキャンバス
	private Dictionary<ulong, Text> name_dict_; // テキストオブジェクト格納辞書

	void Start()
	{
		name_dict_ = new Dictionary<ulong, Text>();	   // 辞書生成
		canvas_ = GameObject.Find("Canvas").transform; // キャンバスを検索しておく
	}
	
	void Update()
	{
		var remove_candidates = new Dictionary<ulong, Text>(name_dict_); // 削除候補辞書

		var player_gos = GameObject.FindGameObjectsWithTag("Player"); // タグPlayerでリストアップ
		if (player_gos != null) {
			foreach (var player_go in player_gos) {
				var player = player_go.GetComponent<Player>();
				if (player.sharedNod == null)
					continue;
				ulong id = player.sharedNod.getGlobalId(); // IDを取得
				Text text;
				if (name_dict_.ContainsKey(id)) { // すでに存在していた
					text = name_dict_[id];		  // 取得
					remove_candidates.Remove(id); // 削除候補から消す
				} else {						  // 存在していなかった
					var go = Instantiate(namePrefab_) as GameObject; // 生成
					text = go.GetComponent<Text>(); // 取得
					go.transform.SetParent(canvas_); // 親を指定
					name_dict_[id] = text;			 // 辞書に登録
				}				
				text.text = player.name_; // テキストに名前を入れる
				var screenPos = Camera.main.WorldToScreenPoint(player_go.transform.position); // 2D位置を取得
				text.transform.position = screenPos; // 設定
			}
		}

		// 消えたものを削除
		foreach (KeyValuePair<ulong, Text> pair in remove_candidates) {
			name_dict_.Remove(pair.Key);	// 辞書から削除
			Destroy(pair.Value.gameObject); // オブジェクト破棄
		}
	}
}
