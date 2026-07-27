using O2un.Core;
using O2un.Core.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace O2un.Utils
{
    public static class VisualElementTranslateExtension
    {
        public static VisualElementBinding<VisualElement> RegistDrag(this VisualElement root, string handle, string window)
        {
            var header = root.QRequiredBindingByClass<VisualElement>(handle);
            VisualElement w = root.Q<VisualElement>(className: window);
            header.RegistDrag(w);

            return header;
        }

        private static void RegistDrag(this VisualElementBinding<VisualElement> dragHandle, VisualElement targetWindow, bool clampToParent = true)
        {
            dragHandle.ThrowIfNull();
            targetWindow.ThrowIfNull();

            const int InvalidPointerId = -1;

            int activePointerId = InvalidPointerId;
            Vector2 startPointerPosition = Vector2.zero;
            Vector3 startWindowTranslate = Vector3.zero;

            VisualElement handle = dragHandle.Element;

            void EndDrag(int pointerId)
            {
                if (pointerId != activePointerId)
                {
                    return;
                }

                if (handle.HasPointerCapture(pointerId))
                {
                    handle.ReleasePointer(pointerId);
                }

                activePointerId = InvalidPointerId;
            }

            dragHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (activePointerId != InvalidPointerId)
                {
                    return;
                }

                if (!evt.isPrimary || evt.button != 0)
                {
                    return;
                }

                if (evt.target is VisualElement eventTarget &&
                    eventTarget.GetFirstOfType<Button>() != null)
                {
                    return;
                }

                if (targetWindow.parent == null)
                {
                    return;
                }

                activePointerId = evt.pointerId;
                startPointerPosition = evt.position;
                startWindowTranslate = targetWindow.resolvedStyle.translate;

                handle.CapturePointer(activePointerId);
                evt.StopPropagation();
            });

            dragHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != activePointerId)
                {
                    return;
                }

                if (!handle.HasPointerCapture(activePointerId))
                {
                    return;
                }

                if (targetWindow.parent == null)
                {
                    EndDrag(activePointerId);
                    return;
                }

                Vector2 delta = (Vector2)evt.position - startPointerPosition;

                Vector3 nextTranslate = new(
                    startWindowTranslate.x + delta.x,
                    startWindowTranslate.y + delta.y,
                    startWindowTranslate.z);

                if (clampToParent)
                {
                    nextTranslate = targetWindow.ClampTranslateToParent(nextTranslate);
                }

                targetWindow.style.translate = nextTranslate;
                evt.StopPropagation();
            });

            dragHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != activePointerId)
                {
                    return;
                }

                EndDrag(evt.pointerId);
                evt.StopPropagation();
            });

            dragHandle.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (evt.pointerId != activePointerId)
                {
                    return;
                }

                EndDrag(evt.pointerId);
            });

            dragHandle.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId == activePointerId)
                {
                    activePointerId = InvalidPointerId;
                }
            });

            dragHandle.Track(
                state: 0,
                subscribe: static (_, _) => { },
                unsubscribe: (element, _) =>
                {
                    if (activePointerId != InvalidPointerId &&
                        element.HasPointerCapture(activePointerId))
                    {
                        element.ReleasePointer(activePointerId);
                    }

                    activePointerId = InvalidPointerId;
                });
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && 0f < value;
        }

        public static Vector2 ClampTranslateToParent(this VisualElement element, Vector3 translate)
        {
            element.ThrowIfNull();

            VisualElement parent = element.parent;
            if (parent == null)
            {
                return translate;
            }

            Rect parentRect = parent.worldBound;
            Rect elementRect = element.worldBound;

            Vector3 currentTranslate = element.resolvedStyle.translate;
            Vector2 requestedDelta = new(translate.x - currentTranslate.x, translate.y - currentTranslate.y);

            Rect requestedRect = elementRect;
            requestedRect.position += requestedDelta;

            if (requestedRect.xMin < parentRect.xMin)
            {
                translate.x += parentRect.xMin - requestedRect.xMin;
            }
            else if (requestedRect.xMax > parentRect.xMax)
            {
                translate.x -= requestedRect.xMax - parentRect.xMax;
            }

            if (requestedRect.yMin < parentRect.yMin)
            {
                translate.y += parentRect.yMin - requestedRect.yMin;
            }
            else if (requestedRect.yMax > parentRect.yMax)
            {
                translate.y -= requestedRect.yMax - parentRect.yMax;
            }

            return translate;
        }

        public static void CenterInParent(this VisualElement element)
        {
            element.ThrowIfNull();
            var parent = element.parent;
            if (parent == null)
            {
                return;
            }

            void Apply()
            {
                Rect parentRect = parent.worldBound;
                Rect elementRect = element.worldBound;

                if (parentRect.width <= 0f || parentRect.height <= 0f || elementRect.width <= 0f || elementRect.height <= 0f)
                {
                    return;
                }

                Vector2 offset = parentRect.center - elementRect.center;
                Vector3 position = element.resolvedStyle.translate;

                element.style.translate = new Vector3(position.x + offset.x, position.y + offset.y, position.z);
            }

            if (element.panel != null && element.worldBound.width > 0f && element.worldBound.height > 0f)
            {
                Apply();
                return;
            }

            EventCallback<GeometryChangedEvent> callback = null;
            callback = _ =>
            {
                element.UnregisterCallback(callback);
                Apply();
            };

            element.RegisterCallback(callback);
        }

        public static void BringToFrontAndCenter(this VisualElement element, string window)
        {
            element.ThrowIfNull();
            element.BringToFront();

            VisualElement w = element.Q<VisualElement>(className: window);
            w.CenterInParent();
        }
    }
}
