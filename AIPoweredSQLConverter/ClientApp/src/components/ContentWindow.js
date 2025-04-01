import React, { useState, useEffect } from 'react';
import './ContentWindow.css';

function ContentWindow({ tableDefinitions, onAssistantSend, onSave }) {
    const [localTableDefinitions, setLocalTableDefinitions] = useState('');
    const [assistantInput, setAssistantInput] = useState('');

    useEffect(() => {
        setLocalTableDefinitions(tableDefinitions);
    }, [tableDefinitions]);

    const handleInputChange = (event) => {
        const value = event.target.value;
        setLocalTableDefinitions(value);
    };

    const handleAssistantInputChange = (event) => {
        const value = event.target.value;
        setAssistantInput(value);
    };

    const handleSend = () => {
        onAssistantSend(assistantInput);
        setAssistantInput('');
    };

    const handleSave = () => {
        onSave(localTableDefinitions);
    };

    const handleKeyDown = (event) => {
        if (event.key === 'Enter') {
            event.preventDefault();
            handleSend();
        }
    };

    return (
        <div className="component-container">
            <label className="table-definitions-label">SQL Table Schema(s)</label>
            <textarea
                className="table-definitions-input"
                value={localTableDefinitions}
                onChange={handleInputChange}
                rows="10"
                cols="50"
            />
            <div className="table-definitions-toolbar">
                <div className="assistant-container">
                    <label className="assistant-label">
                        SQL Table Construction Assistant
                    </label>
                    <input
                        className="assistant-input"
                        type="text"
                        placeholder="Describe your table(s) here to generate a template, or get help modifying an existing one..."
                        value={assistantInput}
                        onChange={handleAssistantInputChange}
                        onKeyDown={handleKeyDown}
                    />
                </div>
                <button className="toolbar-button" onClick={handleSend}>Send</button>
                <button className="toolbar-button" onClick={handleSave}>Save</button>
            </div>
        </div>
    );
}

export default ContentWindow;
