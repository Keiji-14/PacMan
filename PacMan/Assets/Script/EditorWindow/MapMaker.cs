using UnityEngine;
using UnityEditor;

/// <summary>
/// マップ制作を行うエディターツール
/// </summary>
public class MapMaker : EditorWindow
{
    #region PrivateField
    /// <summary>Readmeの表示・非表示の判定</summary>
    private bool isShowReadme;
    /// <summary>オブジェクト設置モードの判定</summary>
    private bool isSetObjectMethod;
    /// <summary>オブジェクト削除モードの判定</summary>
    private bool isDestroyObjectMethod;
    /// <summary>グリッド線の間隔</summary>
    private float gridSize = 2.0f;
    /// <summary>グリッド線の表示範囲</summary>
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
        // MapMakerウィンドウを開く
        var window = GetWindow<MapMaker>();
        window.titleContent = new GUIContent("MapMaker");
    }

    void OnGUI()
    {
        // ステージと設置オブジェクトを選択するフィールドを表示
        stageTransform = (Transform)EditorGUILayout.ObjectField("ステージ", stageTransform, typeof(Transform), true);
        objectToGenerate = (GameObject)EditorGUILayout.ObjectField("設置オブジェクト", objectToGenerate, typeof(GameObject), true);

        EditorGUILayout.Space();
        // グリッドサイズと範囲を設定するフィールドを表示
        gridSize = EditorGUILayout.FloatField("グリッド線", gridSize);
        gridRange = EditorGUILayout.FloatField("グリッド範囲", gridRange);

        EditorGUILayout.Space();
        SetObjectScaleField();
        
        EditorGUILayout.BeginHorizontal();
        // オブジェクト設置モードの切り替えボタン
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

            // 設置モードの状態を切り替え
            isSetObjectMethod = !isSetObjectMethod;
        }

        // オブジェクト削除モードの切り替えボタン
        if (GUILayout.Button(isDestroyObjectMethod ? "オブジェクト削除モード解除" : "オブジェクト削除モード"))
        {
            if (isDestroyObjectMethod)
            {
                // オブジェクト削除メソッドの解除
                SceneView.duringSceneGui -= OnDestroyObjectGUI;
            }
            else
            {
                // オブジェクト削除メソッドの登録
                SceneView.duringSceneGui += OnDestroyObjectGUI;
                if (isSetObjectMethod)
                {
                    // オブジェクト設置メソッドの解除
                    SceneView.duringSceneGui -= OnSetObject;
                    isSetObjectMethod = !isSetObjectMethod;
                    // プレビューオブジェクトを削除
                    DestroyImmediate(previewObject);
                }
            }
        }

        // 削除モードの状態を切り替え
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

        // Readmeの表示・非表示を管理
        isShowReadme = EditorGUILayout.Foldout(isShowReadme, "Readme");
        if (isShowReadme)
        {
            ReadmeBoxField();
        }

        // プレビューオブジェクトのサイズを更新
        UpdatePreviewObjectScale();

        // 画面の再描画を強制
        Repaint();
    }

    /// <summary>
    /// オブジェクトのスケールを設定するフィールドを表示する処理
    /// </summary>
    private void SetObjectScaleField()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("オブジェクトのサイズ", GUILayout.Width(150));

        GUILayout.FlexibleSpace();

        // スケールの各軸の値を入力するフィールドを表示
        Vector3 newScale = objectScale;

        GUILayout.Label("X", GUILayout.Width(12));
        newScale.x = EditorGUILayout.FloatField(objectScale.x);
        GUILayout.Label("Y", GUILayout.Width(12));
        newScale.y = EditorGUILayout.FloatField(objectScale.y);
        GUILayout.Label("Z", GUILayout.Width(12));
        newScale.z = EditorGUILayout.FloatField(objectScale.z);

        // 新しいスケール値を設定
        objectScale = newScale;

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// オブジェクトを設置する処理
    /// </summary>
    /// <param name="sceneView">現在のシーンビュー</param>
    void OnSetObject(SceneView sceneView)
    {
        // グリッド線を描画
        DrawGrid();
        SetObjectRotation();
        SetPreviewPosition(sceneView);
        
        // クリック位置を基にレイを計算
        Vector3 mousePosition = Event.current.mousePosition;
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo))
        {
            // レイがヒットした位置をグリッドに合わせて計算
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
                // Undo操作の登録
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
        // オブジェクト設置モードかつキー入力がある場合の処理
        if (isSetObjectMethod && Event.current.type == EventType.KeyDown)
        {
            // Aキーでオブジェクトを左回転させる
            if (Event.current.keyCode == KeyCode.A)
            {
                objectPlacementRotation *= Quaternion.Euler(0f, -90f, 0f);
                Event.current.Use();
            }
            // Dキーでオブジェクトを右回転させる
            if (Event.current.keyCode == KeyCode.D)
            {
                objectPlacementRotation *= Quaternion.Euler(0f, 90f, 0f);
                Event.current.Use();
            }
            // プレビューオブジェクトにも回転を適用
            if (previewObject != null)
            {
                previewObject.transform.rotation = objectPlacementRotation;
            }
        }
    }

    /// <summary>
    /// 配置する位置にプレビュー表示で可視化する処理
    /// </summary>
    /// <param name="position">プレビューオブジェクトの位置</param>
    /// <param name="quaternion">プレビューオブジェクトの回転</param>
    private void SetPreviewObject(Vector3 position, Quaternion quaternion)
    {
        // 既存のプレビューオブジェクトが異なる場合、古いプレビューを削除
        if (previewObject != null && previewObject.name != objectToGenerate.name + "(Clone)")
        {
            DestroyImmediate(previewObject);
            previewObject = null;
        }
        // プレビューオブジェクトがまだ生成されていない場合、新しいものを生成
        if (previewObject == null && objectToGenerate != null)
        {
            previewObject = Instantiate(objectToGenerate, position, quaternion);
            previewObject.name = objectToGenerate.name + "(Clone)";
            previewObject.transform.localScale = objectScale;
        }
        // プレビューオブジェクトが存在する場合、位置と回転を更新
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

    /// <summary>
    /// プレビューオブジェクトの位置と回転情報をシーンビューに表示する処理
    /// </summary>
    /// <param name="sceneView">現在のシーンビュー</param>
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
    /// オブジェクトを削除する処理
    /// </summary>
    /// <param name="sceneView">現在のシーンビュー</param>
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
        // グリッド線の色を灰色に設定
        Handles.color = Color.gray;
        for (float x = -gridRange; x <= gridRange; x += gridSize)
        {
            for (float z = -gridRange; z <= gridRange; z += gridSize)
            {
                // 水平方向のグリッド線を描画
                Vector3 from = new Vector3(x, 0, z);
                Vector3 to = new Vector3(x, 0, z + gridSize);
                Handles.DrawLine(from, to);

                // 垂直方向のグリッド線を描画
                from = new Vector3(x + gridSize, 0, z);
                to = new Vector3(x, 0, z);
                Handles.DrawLine(from, to);
            }
        }
    }

    /// <summary>
    /// 制作中のステージの子オブジェクトを削除する
    /// </summary>
    /// <param name="stageChild">削除対象のステージオブジェクトの親</param>
    private void DestroyStage(Transform stageChild)
    {
        // 指定された親のすべての子オブジェクトを削除
        for (int i = stageChild.childCount - 1; i >= 0; i--)
        {
            Transform child = stageChild.GetChild(i);
            DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>
    /// Readmeボックスを表示する処理
    /// </summary>
    private void ReadmeBoxField()
    {
        EditorGUILayout.HelpBox(
            "1. オブジェクトの配置が行えるように、Planeの生成を行う。（ステージの大きさによってサイズを調整）\n\n" +
            "2. ステージのオブジェクトの選択する項目に、親オブジェクトとなるオブジェクトをアタッチする。\n\n" +
            "3. 配置したいオブジェクトを設置オブジェクトの選択する項目にアタッチする。\n\n" +
            "4. オブジェクトのサイズや合わせてグリッド線の調整を行う。\n\n" +
            "5. オブジェクトのサイズとグリッド線の調整が完了後、「オブジェクト設置モード」のボタンを押す。\n\n" +
            "6. 配置したい場所に合わせてマウスを左クリックで設置を行う。",
            MessageType.None);
    }
}