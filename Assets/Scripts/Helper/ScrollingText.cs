using TMPro;
using UnityEngine;

public class ScrollingText : MonoBehaviour
{
    public float scrollSpeed = 0.5f; // Скорость скроллинга (секунды на символ)
    public int visibleCharacters = 5; // Количество видимых символов

    private TMP_Text textComponent;
    private string fullText = "";
    private int currentOffset = 0;
    private float timer = 0f;

    void Start()
    {
        textComponent = GetComponent<TMP_Text>();
        // Инициализируем текст, если он был задан заранее
        if (!string.IsNullOrEmpty(fullText))
        {
            UpdateDisplay();
        }
    }

    void Update()
    {
        if (fullText.Length <= visibleCharacters) return;

        timer += Time.deltaTime;

        if (timer >= scrollSpeed)
        {
            timer = 0f;
            currentOffset++;

            // Сброс offset при достижении конца текста
            if (currentOffset > fullText.Length) currentOffset = 0;

            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        string displayedText = "";
        int remainingChars = visibleCharacters;

        // Добавляем символы от текущего offset до конца строки
        if (currentOffset < fullText.Length)
        {
            displayedText = fullText.Substring(currentOffset);
            remainingChars -= fullText.Length - currentOffset;
        }

        // Добавляем символы из начала строки если нужно
        if (remainingChars > 0 && fullText.Length >= remainingChars)
        {
            displayedText += fullText.Substring(0, remainingChars);
        }

        // Обрезаем до нужной длины если необходимо
        if (displayedText.Length > visibleCharacters)
        {
            displayedText = displayedText.Substring(0, visibleCharacters);
        }

        if(textComponent != null && textComponent.text != null)
            textComponent.text = displayedText;
    }

    public void SetText(string newText)
    {
        fullText = newText;
        currentOffset = 0;
        timer = 0f;
        UpdateDisplay();
    }
}