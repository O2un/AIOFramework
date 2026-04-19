using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Method)]
public class TestButton : PropertyAttribute
{
    public string ButtonName {get;}
    public TestButton(string name = null) => ButtonName = name;
}
