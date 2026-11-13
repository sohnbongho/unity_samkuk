# 유니티 네임스페이스를 호환 층으로 바꾸는 규칙 (copy_core.sh, copy_tests.sh 가 함께 쓴다)
s/\r$//
s/^using UnityEngine;$/using Samkuk.Compat;/
/^using UnityEngine\.TestTools;$/d
/^using UnityEditor;$/d
/^using UnityEditor\.SceneManagement;$/d
/^using UnityEngine\.SceneManagement;$/d
/^using UnityEngine\.Rendering;$/d
/^using UnityEngine\.Rendering\.Universal;$/d
s/^using UnityEngine\.UI;$/using Samkuk.Compat.UI;/
s/^using UnityEngine\.EventSystems;$/using Samkuk.Compat.UI;/
s/^using UnityEngine\.InputSystem\.UI;$/using Samkuk.Compat.UI;/
s/^using UnityEngine\.InputSystem;$/using Samkuk.Compat.InputSystem;/
s/^using UnityEngine\.Events;$/using Samkuk.Compat.Events;/
s/^using UnityEngine\.Pool;$/using Samkuk.Compat.Pool;/
s/UnityEngine\.Events\./Samkuk.Compat.Events./g
s/UnityEngine\.InputSystem\./Samkuk.Compat.InputSystem./g
s/UnityEngine\.UI\./Samkuk.Compat.UI./g
s/UnityEngine\.SceneManagement\.//g
s/UnityEngine\.Rendering\.Universal\.//g
s/UnityEngine\.Rendering\.//g
s/UnityEditor\.AssetDatabase/AssetDatabase/g
s/\[UnityEngine\./[Samkuk.Compat./g
s/UnityEngine\./Samkuk.Compat./g
