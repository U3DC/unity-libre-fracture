using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class JointMonitor : MonoBehaviour
{
    [Serializable]
    public class JointRemovedEvent : UnityEvent<Rigidbody> { }

    public UnityAction<Rigidbody> onJointRemove;

    [SerializeField]
    JointRemovedEvent m_RemovedEvent = new JointRemovedEvent();

    List<Rigidbody> m_rigidbodies;
    List<Joint> m_MonitoredJoints;

    void Start()
    {
        m_MonitoredJoints = new List<Joint>(1);
        GetComponents<Joint>(m_MonitoredJoints);
        m_rigidbodies = new List<Rigidbody>(m_MonitoredJoints.Count);
    }

    void FixedUpdate()
    {
        /*for (var i = 0; i < m_MonitoredJoints.Count; ++i)
        {
            var joint = m_MonitoredJoints[i];
            if (joint)
            {
                if (!m_rigidbodies.Contains(joint.connectedBody))
                    m_rigidbodies.Add(joint.connectedBody);
                
                continue;
            }

            onJointRemove.Invoke(m_rigidbodies[i]);
            m_RemovedEvent.Invoke(m_rigidbodies[i]);
            m_MonitoredJoints.RemoveAt(i);
        }*/
    }
}