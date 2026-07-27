using System;
using O2un.Core;
using O2un.Core.Utils;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace O2un.Utils
{
    public static class VisualElementInstance
    {
        public static TemplateContainer InstantiatePopup(this VisualTreeAsset asset, VisualElement root, CompositeDisposable popupLifetime, Action closeAction, string bg = "", string popUpWindow = "", string closeButtonName = "")
        {
            asset.ThrowIfNull();
            root.ThrowIfNull();
            popupLifetime.ThrowIfNull();
            closeAction.ThrowIfNull();

            if (string.IsNullOrWhiteSpace(closeButtonName))
            {
                closeButtonName = "closeButton";
            }

            if (string.IsNullOrWhiteSpace(popUpWindow))
            {
                popUpWindow = "popup-window";
            }

            if (string.IsNullOrWhiteSpace(bg))
            {
                bg = "popupContainer";
            }

            var instance = asset.Instantiate();
            instance.StretchToParentSize();
            instance.pickingMode = PickingMode.Ignore;

            var popupContainer = instance.QOptional<VisualElement>(bg);
            if (popupContainer != null)
            {
                popupContainer.style.backgroundColor = Color.clear;
                popupContainer.pickingMode = PickingMode.Ignore;
            }

            var popupWindow = instance.QRequired<VisualElement>(popUpWindow);
            popupWindow.pickingMode = PickingMode.Position;

            var closeButton = instance.QOptionalBinding<Button>(closeButtonName);
            if(null != closeButton)
            {
                closeButton.Clicked(closeAction).AddTo(popupLifetime);
            }

            root.Add(instance);
            Disposable.Create(instance.RemoveFromHierarchy).AddTo(popupLifetime);
            return instance;
        }
    }
}
