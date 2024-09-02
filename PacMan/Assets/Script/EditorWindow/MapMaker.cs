using UnityEngine;
using UnityEditor;

public class MapMaker : EditorWindow
{
    #region PrivateField
    private bool isShowReadme;
    private bool isSetObjectMethod;
    private bool isDestroyObjectMethod;
    /// <summary>グリッドのサイズ</summary>
    private float gridSize = 2.0f;
    /// <summary>グリッドの表示範囲</summary>
    private float gridRange = 20.0f;
    /// <summary>オブジェクトのスケール設定</summary>
    private Vector3 objectScale = Vector3.one;
    /// <summary>オブジェクトの配置位置</summary>
    private Vector3 objectPlacementPosition;
    /// <summary>オブジェクトの配置回転</summary>
    private Quaternion objectPlacementRotation = Quaternion.identity;
    /// <summary>ステージを管理する親オブジェクト</summary>
    private Transform stageTransform;
    /// <summary>配置を行う際のプレビュー表示</summary>
    private GameObject previewObject;
    /// <summary>配置を行うオブジェクト</summary>
    private GameObject objectToGenerate;
    #endregion

    [MenuItem("Window/MapMaker")]
    static void Open()
    {
        var window = GetWindow<MapMaker>();
        window.titleContent = new GUIContent("MapMaker");
    }

    void OnGUI()
    {
        stageTransform = (Transform)EditorGUILayout.ObjectField("ステージ", stageTransform, typeof(Transform), true);
        objectToGenerate = (GameObject)EditorGUILayout.ObjectField("設置オブジェクト", objectToGenerate, typeof(GameObject), true);

        EditorGUILayout.Space();
        gridSize = EditorGUILayout.FloatField("グリッド線", gridSize);
        gridRange = EditorGUILayout.FloatField("グリッド範囲", gridRange);

        EditorGUILayout.Space();
        DrawLinkedScaleField();
        
        EditorGUILayout.BeginHorizontal();
        // 登録と解除の切り替えボタン
        if (GUILayout.Button(isSetObjectMethod ? "オブジェクト設置モード解除" : "オブジェクト設置モード"))
        {
            if (isSetObjectMethod)
            {
                // オブジェクト設置メソッドの解除
                SceneView.duringSceneGui -= OnSetObject;
                // プレビューオブジェクトを削除
                DestroyImmediate(previewObject);
            }
            else
            {
                // オブジェクト設置メソッドの登録
                SceneView.duringSceneGui += OnSetObject;
                if (isDestroyObjectMethod)
                {
                    // オブジェクト削除メソッドの解除
                    SceneView.duringSceneGui -= OnDestroyObjectGUI;
                    isDestroyObjectMethod = !isDestroyObjectMethod;
                }
            }
            
            isSetObjectMethod = !isSetObjectMethod; // 状態を切り替え
        }
        if (GUILayout.Button(isDestroyObjectMethod ? "オブジェクト削除モード解除" : "オブジェクト削除モード"))
        {
            if (isDestroyObjectMethod)
            {
                SceneView.duringSceneGui -= OnDestroyObjectGUI;
            }
            else
            {
                SceneView.duringSceneGui += OnDestroyObjectGUI;
                if (isSetObjectMethod)
                {
                    SceneView.duringSceneGui -= OnSetObject;
                    isSetObjectMethod = !isSetObjectMethod;
                    // プレビューオブジェクトを削除
                    DestroyImmediate(previewObject);
                }
            }
        }
        
        isDestroyObjectMethod = !isDestroyObjectMethod;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        if (GUILayout.Button("ステージを削除"))
        {
            if (stageTransform != null)
            {
                bool confirmed = EditorUtility.DisplayDialog("確認", "本当にステージを削除しますか？\n一度、プレハブ化することをおすすめします", "OK", "キャンセル");
                if (confirmed)
                {
                    DestroyStage(stageTransform);
                }
            }
            else
            {
                Debug.LogWarning("ステージがnullです。");
            }
        }
        EditorGUILayout.Space();

        isShowReadme = EditorGUILayout.Foldout(isShowReadme, "Readme");
        if (isShowReadme)
        {
            EditorGUILayout.HelpBox("ここにReadmeの内容を記述します。", MessageType.None);

            // ここにReadmeの具体的なコンテンツを記述します。
        }

        // プレビューオブジェクトのサイズを反映
        UpdatePreviewObjectScale();

        // 画面の再描画を強制
        Repaint();
    }

    private void DrawLinkedScaleField()
    {
        EditorGUILayout.BeginHorizontal();
        // スケールの各軸の値を表示
        EditorGUILayout.LabelField("オブジェクトのサイズ", GUILayout.Width(150));

        GUILayout.FlexibleSpace();
        
        // スケール入力フィールドとラベルを並べる
        Vector3 newScale = objectScale;

        GUILayout.Label("X", GUILayout.Width(12));
        newScale.x = EditorGUILayout.FloatField(objectScale.x);
        GUILayout.Label("Y", GUILayout.Width(12));
        newScale.y = EditorGUILayout.FloatField(objectScale.y);
        GUILayout.Label("Z", GUILayout.Width(12));
        newScale.z = EditorGUILayout.FloatField(objectScale.z);

        objectScale = newScale;

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// オブジェクトを設置する処理
    /// </summary>
    void OnSetObject(SceneView sceneView)
    {
        // グリッド線を描画
        DrawGrid();
        SetObjectRotation();
        SetPreviewPosition(sceneView);
        
        // クリック位置をグリッドに合わせて計算
        Vector3 mousePosition = Event.current.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo))
        {
            objectPlacementPosition = hitInfo.point + hitInfo.normal * 0.5f;

            objectPlacementPosition.x = Mathf.Floor(objectPlacementPosition.x / gridSize) * gridSize + gridSize;
            objectPlacementPosition.y = 0.0f;
            objectPlacementPosition.z = Mathf.Floor(objectPlacementPosition.z / gridSize) * gridSize + gridSize;

            // プレビューオブジェクトの位置と回転を設定
            SetPreviewObject(objectPlacementPosition, objectPlacementRotation);

            if (objectToGenerate != null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                // オブジェクトの生成とUndoの登録
                GameObject newObject = Instantiate(objectToGenerate, objectPlacementPosition, objectPlacementRotation, stageTransform);
                // サイズを設定
                newObject.transform.localScale = objectScale;
                Undo.RegisterCreatedObjectUndo(newObject, "Place Object");
                Event.current.Use();
            }
        }
    }

    /// <summary>
    /// 配置するオブジェクトを回転させる処理
    /// </summary>
    private void SetObjectRotation()
    {
        var currentRotation = objectPlacementRotation;

        if (isSetObjectMethod && Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.A)
            {
                objectPlacementRotation *= Quaternion.Euler(0f, -90f, 0f);
                Event.current.Use();
            }

            if (Event.current.keyCode == KeyCode.D)
            {
                objectPlacementRotation *= Quaternion.Euler(0f, 90f, 0f);
                Event.current.Use();
            }

            // 回転を適用
            if (previewObject != null)
            {
                previewObject.transform.rotation = objectPlacementRotation;
            }
        }
    }

    /// <summary>
    /// 配置する位置にプレビュー表示で可視化する処理
    /// </summary>
    private void SetPreviewObject(Vector3 position, Quaternion quaternion)
    {
        if (previewObject != null && previewObject.name != objectToGenerate.name + "(Clone)")
        {
            // 古いプレビューオブジェクトを削除
            DestroyImmediate(previewObject);
            previewObject = null;
        }

        // プレビューオブジェクトがまだ生成されていない場合、新しいものを生成
        if (previewObject == null && objectToGenerate != null)
        {
            previewObject = Instantiate(objectToGenerate, position, quaternion);
            previewObject.name = objectToGenerate.name + "(Clone)";
            previewObject.hideFlags = HideFlags.HideAndDontSave;
            previewObject.transform.localScale = objectScale;
        }

        if (previewObject != null)
        {
            previewObject.transform.position = position;
            previewObject.transform.rotation = quaternion;
        }
    }

    /// <summary>
    /// プレビュー表示のオブジェクトのサイズを反映する処理
    /// </summary>
    private void UpdatePreviewObjectScale()
    {
        if (previewObject != null)
        {
            // プレビューオブジェクトのスケールを更新
            previewObject.transform.localScale = objectScale;
        }
    }

    private void SetPreviewPosition(SceneView sceneView)
    {
        // 座標確認UIの位置を右下に指定
        Vector2 guiPosition = new Vector2(sceneView.position.width - 70, sceneView.position.height - 150);

        // 座標を表示するテキストのスタイル
        GUIStyle textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.white;

        // 回転をオイラー角に変換
        Vector3 rotationEulerAngles = objectPlacementRotation.eulerAngles;

        // 座標を表示
        Handles.BeginGUI();
        GUI.Label(new Rect(guiPosition.x, guiPosition.y, 100, 20),
            "Position:" +
            "\nX: " + objectPlacementPosition.x.ToString("F2") + 
            "\nY: " + objectPlacementPosition.y.ToString("F2") + 
            "\nZ: " + objectPlacementPosition.z.ToString("F2") +
            "\nRotation:" +
            "\nX: " + rotationEulerAngles.x.ToString("F2") +
            "\nY: " + rotationEulerAngles.y.ToString("F2") +
            "\nZ: " + rotationEulerAngles.z.ToString("F2"), textStyle);
            
        Handles.EndGUI();
    }

    /// <summary>
    /// オブジェクトの重なりを確認する処理
    /// </summary>
    private bool CheckObjectOverlap(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, gridSize / 2f);

        foreach (var collider in colliders)
        {
            if (collider.gameObject != objectToGenerate && collider.gameObject != stageTransform.gameObject)
            {
                // 他のオブジェクトと重なっている場合は true を返す
                return true; 
            }
        }

        // 重なりがない場合は false を返す
        return false;
    }

    /// <summary>
    /// オブジェクトを削除する処理
    /// </summary>
    private void OnDestroyObjectGUI(SceneView sceneView)
    {
        // グリッド線を描画
        DrawGrid();

        // クリック位置をグリッドに合わせて計算
        Vector3 clickPosition = Event.current.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(clickPosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo))
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                // レイがヒットしたオブジェクトを取得
                GameObject objectToDelete = hitInfo.collider.gameObject;

                // レイヤーマスクを使って削除対象のオブジェクトが特定のレイヤーに含まれるか確認することもできます
                if (objectToDelete.layer == LayerMask.NameToLayer("StageObject") && objectToDelete != null)
                {
                    // Undoの登録と削除
                    Undo.DestroyObjectImmediate(objectToDelete);
                    Event.current.Use();
                }
            }
        }
    }

    /// <summary>
    /// グリッド線を引く
    /// </summary>
    private void DrawGrid()
    {
        Handles.color = Color.gray;
        for (float x = -gridRange; x <= gridRange; x += gridSize)
        {
            for (float z = -gridRange; z <= gridRange; z += gridSize)
            {
                Vector3 from = new Vector3(x, 0, z);
                Vector3 to = new Vector3(x, 0, z + gridSize);
                Handles.DrawLine(from, to);

                from = new Vector3(x + gridSize, 0, z);
                to = new Vector3(x, 0, z);
                Handles.DrawLine(from, to);
            }
        }
    }

    /// <summary>
    /// 制作中のステージの子オブジェクトを削除する
    /// </summary>
    private void DestroyStage(Transform stageChild)
    {
        for (int i = stageChild.childCount - 1; i >= 0; i--)
        {
            Transform child = stageChild.GetChild(i);
            DestroyImmediate(child.gameObject);
        }
    }
}