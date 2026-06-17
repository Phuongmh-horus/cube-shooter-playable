using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType(typeof(T)) as T;
            }

            if (_instance == null)
            {
                // Debug.LogWarning("There is no instance of " + typeof(T).Name + " in the scene");
                // GameObject go = new GameObject(nameof(T) + "(MonoSingleton)");
                // _instance = Instantiate(go).AddComponent<T>();
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
    }
}
