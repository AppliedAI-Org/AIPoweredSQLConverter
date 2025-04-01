import React, { useState } from 'react';
import ApiClient from '../api/ApiClient';
import { useAuth0 } from '@auth0/auth0-react';
import authConfig from '../auth_config.json';
import './GenerateApiKey.css';

const GenerateApiKey = () => {
    const { user, getAccessTokenSilently, isAuthenticated } = useAuth0();
    const [apiKey, setApiKey] = useState('');
    const [error, setError] = useState('');

    const generateApiKey = async () => {
        if (!isAuthenticated) {
            alert('You must log in to access the API');
            return;
        }

        const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);
        try {
            const newApiKey = await apiClient.getNewAPIKey(user.sub);
            setApiKey(newApiKey);
            setError('');
        } catch (err) {

        }
    };

    return (
        <div className="generate-api-key-container">
            <button
                className="generate-api-key-button"
                onClick={generateApiKey}
            >
                Generate API Key
            </button>
            <div className="api-key-output-container">
                {apiKey && <p className="api-key-text">{apiKey}</p>}
                {error && <p className="error">{error}</p>}
            </div>
        </div>
    );
};

export default GenerateApiKey;
