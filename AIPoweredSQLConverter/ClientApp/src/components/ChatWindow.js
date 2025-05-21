import React, { useState, useRef } from 'react';
import emailjs from 'emailjs-com';
import './ChatWindow.css';
import { Message } from './Message';

function ChatWindow({ messages, sendMessage, clearMessages }) {
    const [inputMessage, setInputMessage] = useState('');
    const chatEndRef = useRef(null);

    const handleSendMessage = () => {
        sendMessage(inputMessage);
        setInputMessage('');
    };

    const handleFeedback = (type, content) => {
        emailjs.send(
            'service_46i2oxe',
            'template_eukcnxj',
            {
                feedback_type: type,
                feedback_content: content,
            },
            'l4tPGVW0fU2gIrpaI'
        )
    };

    return (
        <div className="chat-window">
            <div className="message-list">
                {messages.map((msg, i) => (
                    <Message
                        key={i}
                        author={msg.role}
                        content={msg.content}
                        timestamp={msg.timestamp}
                        alignRight={msg.role === 'Assistant'}
                        onFeedback={handleFeedback}
                    />
                ))}
                <div ref={chatEndRef} />
            </div>
            <div className="chat-input">
                <div className="chat-input-input-container">
                    <label>SQL Conversion Assistant</label>
                    <input
                        type="text"
                        value={inputMessage}
                        onChange={e => setInputMessage(e.target.value)}
                        placeholder="Enter a natural language query to convert to SQL..."
                        onKeyDown={e => e.key === 'Enter' && handleSendMessage()}
                    />
                </div>
                <button className="toolbar-button" onClick={handleSendMessage}>
                    Convert
                </button>
                <button className="toolbar-button" onClick={clearMessages}>
                    Reset
                </button>
            </div>
        </div>
    );
}

export default ChatWindow;
