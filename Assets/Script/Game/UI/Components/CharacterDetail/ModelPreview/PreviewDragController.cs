using UnityEngine;
using UnityEngine.EventSystems;

public class PreviewDragController : MonoBehaviour, IDragHandler, IScrollHandler
{
    public Transform cameraPivot; // Camera父物体
    public Camera previewCamera;

    public RectTransform rawImage;
    float yaw;
    float pitch;

    float distance = 2f;

    const float rotateSpeed = 0.2f;
    const float zoomSpeed = 0.3f;

    const float minPitch = -30f;
    const float maxPitch = 45f;

    const float minDistance = 1.2f;
    const float maxDistance = 3.5f;

    public void OnDrag(PointerEventData eventData)
    {
        yaw += eventData.delta.x * rotateSpeed;
        pitch -= eventData.delta.y * rotateSpeed;

        ModelViewer.Instance.Drag(eventData.delta);
    }

    public void OnScroll(PointerEventData eventData)
    {
        distance = eventData.scrollDelta.y;
        Vector2 viewportPos = ConvertToPos(eventData);
        ModelViewer.Instance.Scroll(distance,viewportPos);
    }

    public Vector2 ConvertToPos(PointerEventData eventData)
    {
        RectTransform rectTransform = rawImage;
        Vector2 localPos;

        Camera uiCamera = eventData.pressEventCamera;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,eventData.position,uiCamera,out localPos);

        //转化到0-1的viewport坐标
        Vector2 pivotOffset = rectTransform.pivot;
        Vector2 viewportPos = new Vector2(
            localPos.x/rectTransform.rect.width + pivotOffset.x,
            localPos.y/rectTransform.rect.height + pivotOffset.y);
        
        if(viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1)
            return viewportPos;
        return default;
    }
    
    
    
}
