import React, { Component } from 'react';
import './Message.css';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import { FeedbackButtons } from './FeedbackButtons';

export class Message extends Component {
    static displayName = Message.name;

    handleFeedback = (type) => {
        if (this.props.onFeedback) {
            this.props.onFeedback(type, this.props.content);
        }
    };

    render() {
        const { author, content, timestamp, alignRight } = this.props;
        const alignmentClass = alignRight ? 'align-right' : 'align-left';
        const messageStyle = {
            backgroundColor: alignRight ? '#ebebeb' : '#007bff'
        };

        return (
            <div className={`message-container ${alignmentClass}`}>
                <p className={`author ${alignmentClass}`}>{author}</p>
                <div className="message" style={messageStyle}>
                    <ReactMarkdown className="message-body" rehypePlugins={[rehypeRaw]}>
                        {content}
                    </ReactMarkdown>
                    {author === "Assistant" && (
                        <FeedbackButtons onFeedback={this.handleFeedback} />
                    )}
                </div>
                <p className={`time-stamp ${alignmentClass}`}>{timestamp}</p>
            </div>
        );
    }
}