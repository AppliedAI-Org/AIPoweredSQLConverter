import React, { useState } from 'react';
import './FeedbackButtons.css';

export function FeedbackButtons({ onFeedback }) {
    const [selected, setSelected] = useState(null);

    const handleFeedback = (type) => {
        setSelected(type);
        if (onFeedback) {
            onFeedback(type);
        }
    };

    return (
        <div className="feedback-buttons">
            <button
                className={`feedback-btn thumbs-up${selected === 'up' ? ' selected clicked' : ''}`}
                onClick={() => handleFeedback('up')}
                disabled={selected !== null}
                aria-label="Thumbs up"
            >
                👍
            </button>
            <button
                className={`feedback-btn thumbs-down${selected === 'down' ? ' selected clicked' : ''}`}
                onClick={() => handleFeedback('down')}
                disabled={selected !== null}
                aria-label="Thumbs down"
            >
                👎
            </button>
        </div>
    );
}
