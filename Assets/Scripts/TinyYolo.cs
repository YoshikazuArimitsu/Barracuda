using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Barracuda;

public class TinyYolo : MonoBehaviour
{
    public NNModel modelAsset;
    private Model m_RuntimeModel;
    private IWorker m_worker;

    // Start is called before the first frame update
    void Start()
    {
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        m_worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, m_RuntimeModel);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
