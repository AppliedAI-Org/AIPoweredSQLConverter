import React, { useEffect, useState } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import { debounce } from 'lodash';
import ChatWindow from './ChatWindow';
import ContentWindow from './ContentWindow';
import FooterSection from './FooterSection';
import ApiClient from '../api/ApiClient';
import { NavMenu } from './NavMenu';
import './WindowWrapper.css';
import authConfig from '../auth_config.json';

// Initialize Stripe with your publishable key
const InnapropriateRequestErrorMessage = "Your last message was flagged as unrelated to SQL. Please check your input.";

const WindowWrapper = () => {
    const { isAuthenticated, getAccessTokenSilently, user } = useAuth0();
    const [state, setState] = useState({
        tableDefinitions: '',
        isSmallViewport: window.innerWidth < 1200,
        stage: 0,
        conversationId: '',
        gameStartMessage: '',
        accessToken: '',
        messages: [],
        connectionError: false,
    });

    // Initialize API client


    // Load messages from localStorage on mount
    useEffect(() => {
        const savedMessages = localStorage.getItem('messages');
        if (savedMessages) {
            setState((prevState) => ({
                ...prevState,
                messages: JSON.parse(savedMessages),
            }));
        }
    }, []);

    // Save messages to localStorage whenever they change
    useEffect(() => {
        localStorage.setItem('messages', JSON.stringify(state.messages));
    }, [state.messages]);

    useEffect(() => {
        const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);
        const initialize = async () => {
            if (isAuthenticated) {
                try {
                    const token = await getAccessTokenSilently({
                        audience: authConfig.audience,
                    });
                    setState((prevState) => ({
                        ...prevState,
                        accessToken: token,
                    }));
                } catch (error) {
                    console.error("Error fetching access token");
                }

                try {
                    const sqlData = await apiClient.getSQLData(user.sub);
                    setState((prevState) => ({
                        ...prevState,
                        tableDefinitions: sqlData,
                    }));
                } catch (error) {
                    console.error('Error fetching SQL data:', error.message);
                }
            }
        };
        initialize();
    }, [isAuthenticated, getAccessTokenSilently, user.sub]);

    useEffect(() => {
        const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);
        const handleStripeCheckoutClick = (event) => {
            if (event.target && event.target.id === 'stripe-checkout-link') {
                event.preventDefault();
                apiClient.redirectToStripeCheckout(user.sub);
            }
        };

        document.addEventListener('click', handleStripeCheckoutClick);

        return () => {
            document.removeEventListener('click', handleStripeCheckoutClick);
        };
    }, [getAccessTokenSilently, user.sub]);

    const requestSQLConversion = async (inputMessage) => {
        if (!user || !user.sub) {
            console.error('User is not authenticated or sub ID is missing');
            return;
        }

        const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);

        const userId = user.sub;
        const newMessage = {
            role: 'User',
            content: inputMessage,
        };
        const updatedMessages = [...state.messages, newMessage];

        setState((prevState) => ({
            ...prevState,
            messages: updatedMessages,
        }));

        try {
            if (state.tableDefinitions === null
                || state.tableDefinitions === ""
                || state.tableDefinitions === "null"
                || state.tableDefinitions === "Enter SQL table Schema(s) here to help the model with context.") {
                const responseMessage = {
                    role: 'Assistant',
                    content: "Please be sure to set valid data in the SQL schema input to the left.",
                };
                setState((prevState) => ({
                    ...prevState,
                    messages: [...prevState.messages, responseMessage],
                }));
            }
            else {
                const response = await apiClient.requestSQLConversion(userId, updatedMessages, state.tableDefinitions);

                if (response) {
                    const responseMessage = {
                        role: 'Assistant',
                        content: response,
                    };
                    setState((prevState) => ({
                        ...prevState,
                        messages: [...prevState.messages, responseMessage],
                    }));
                }
            }
        } catch (error) {
            console.error('Error during SQL conversion request:', error.message);
        }
    };

    const handleAssistantSend = async (assistantInput) => {
        if (!user || !user.sub) {
            console.error('User is not authenticated or sub ID is missing');
            return;
        }

        const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);
        const userId = user.sub;

        try {
            const response = await apiClient.requestSQLDataHelp(userId, assistantInput, state.tableDefinitions);

            if (response.inappropriate) {
                const errorMessage = {
                    role: 'Assistant',
                    content: InnapropriateRequestErrorMessage,
                };
                setState((prevState) => ({
                    ...prevState,
                    messages: [...prevState.messages, errorMessage],
                }));
            } else {
                setState((prevState) => ({
                    ...prevState,
                    tableDefinitions: response.response,
                }));
            }
        } catch (error) {
            console.error('Error during SQL data help request:', error.message);
        }
    };

    const handleSave = async (tableDefinitions) => {
        if (!user || !user.sub) {
            console.error('User is not authenticated or sub ID is missing');
            return;
        }

        const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);
        const userId = user.sub;

        try {
            await apiClient.saveSQLData(userId, tableDefinitions);
        } catch (error) {
            console.error('Error saving SQL data:', error.message);
        }
    };

    // clearMessages resets the messages state and localStorage entry
    const clearMessages = () => {
        setState((prevState) => ({
            ...prevState,
            messages: []
        }));
        localStorage.removeItem('messages');
    };

    return (
        <div className="window-wrapper-container">
            <NavMenu />
            <div className="fixed-pane-container">
                <div className="pane">
                    <ContentWindow
                        tableDefinitions={state.tableDefinitions}
                        onAssistantSend={handleAssistantSend}
                        onSave={handleSave}
                    />
                </div>
                <div className="pane">
                    <label className="chatwindow-label">SQL Query Converter</label>
                    <ChatWindow
                        isSmallViewport={state.isSmallViewport}
                        messages={state.messages}
                        sendMessage={(inputMessage) => {
                            requestSQLConversion(inputMessage);
                        }}
                        clearMessages={clearMessages}
                    />
                </div>
            </div>
            <FooterSection />
        </div>
    );
};

export default WindowWrapper;
