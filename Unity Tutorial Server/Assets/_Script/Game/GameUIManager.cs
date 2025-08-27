using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    public Button[] board_buttons = new Button[9];
    public TextMeshProUGUI status_text;
    public Button match_button;
    public GameObject game_over_panel;
    public TextMeshProUGUI result_text;

    void Start()
    {
        game_over_panel.SetActive(false);
        
        for (int i = 0; i < board_buttons.Length; i++)
        {
            int button_index = i;
            board_buttons[i].onClick.AddListener(() => OnBoardButtonClicked(button_index));
        }
        match_button.onClick.AddListener(OnMatchButtonClicked);

        NetworkManager.Instance.OnBoardUpdate += UpdateBoardUI;
        NetworkManager.Instance.OnTurnUpdate += UpdateStatusText;
        NetworkManager.Instance.OnMatchSuccess += OnMatchSuccess;
        NetworkManager.Instance.OnGameOver += ShowGameOver;
        
        UpdateStatusText();
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnBoardUpdate -= UpdateBoardUI;
            NetworkManager.Instance.OnTurnUpdate -= UpdateStatusText;
            NetworkManager.Instance.OnMatchSuccess -= OnMatchSuccess;
            NetworkManager.Instance.OnGameOver -= ShowGameOver;
        }
    }
    
    void OnBoardButtonClicked(int index)
    {
        if (NetworkManager.Instance.my_player_index == NetworkManager.Instance.current_turn_player_index)
        {
            NetworkManager.Instance.send_place_stone((byte)index);
        }
        else
        {
            Debug.Log("Not your turn!");
        }
    }

    void OnMatchButtonClicked()
    {
        NetworkManager.Instance.send_match_request();
        match_button.interactable = false;
        status_text.text = "Waiting for match...";
    }
    
    void OnMatchSuccess()
    {
        UpdateStatusText();
    }

    void UpdateBoardUI()
    {
        for (int i = 0; i < NetworkManager.Instance.board_state.Length; i++)
        {
            TextMeshProUGUI button_text = board_buttons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (button_text == null) continue;

            byte state = NetworkManager.Instance.board_state[i];
            switch (state)
            {
                case 1: button_text.text = "O"; break;
                case 2: button_text.text = "X"; break;
                default: button_text.text = ""; break;
            }
        }
    }

    void UpdateStatusText()
    {
        if (NetworkManager.Instance.is_game_over) return;

        byte my_index = NetworkManager.Instance.my_player_index;

        if (my_index > 1) 
        {
            status_text.text = "Press Match button";
            return;
        }

        byte current_turn_index = NetworkManager.Instance.current_turn_player_index;
        if (my_index == current_turn_index)
        {
            status_text.text = "Your Turn";
        }
        else
        {
            status_text.text = "Opponent's Turn";
        }
    }
    
    void ShowGameOver(byte winner_index)
    {
        game_over_panel.SetActive(true);
    
        if (winner_index == 0)
        {
            result_text.text = "Draw";
        }
        else if (winner_index == NetworkManager.Instance.my_player_index) 
        {
            result_text.text = "You Win!";
        }
        else
        {
            result_text.text = "You Lose";
        }
    }
}