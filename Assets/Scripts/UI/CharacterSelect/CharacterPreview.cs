using SonTinhThuyTinh.Characters;
using UnityEngine;

namespace SonTinhThuyTinh.UI.CharacterSelect
{
    // Stands a character on the select screen and puts it in the spotlight while its option is highlighted.
    public class CharacterPreview : MonoBehaviour
    {
        [SerializeField] Light spotlight;
        [SerializeField] float highlightedIntensity = 60f;
        [SerializeField] float dimmedIntensity = 6f;
        [Tooltip("Degrees the character turns away from the camera while not highlighted.")]
        [SerializeField] float dimmedYaw = 10f;
        [SerializeField] float blendSpeed = 6f;

        Transform model;
        bool highlighted;

        public void Show(CharacterDefinition character)
        {
            if (model != null) Destroy(model.gameObject);

            // Only the visual part of the player prefab (mesh, Animator, weapon), so no gameplay code or input runs here.
            model = Instantiate(character.PlayerPrefab.Animator.gameObject, transform, false).transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.Euler(0f, dimmedYaw, 0f);
        }

        public void SetHighlighted(bool value) => highlighted = value;

        void Update()
        {
            float blend = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);

            if (spotlight != null)
                spotlight.intensity = Mathf.Lerp(spotlight.intensity, highlighted ? highlightedIntensity : dimmedIntensity, blend);

            if (model != null)
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0f, highlighted ? 0f : dimmedYaw, 0f), blend);
        }
    }
}
