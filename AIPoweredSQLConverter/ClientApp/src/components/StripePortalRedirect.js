import React, { useEffect } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import ApiClient from '../api/ApiClient';
import authConfig from '../auth_config.json';

const StripePortalRedirect = () => {
    const { user, loginWithRedirect, getAccessTokenSilently } = useAuth0();
    

    useEffect(() => {
        const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);
        async function createPortalSession() {
            if (!user || !user.sub) {
                loginWithRedirect({
                    authorizationParams: {
                        audience: authConfig.ApiUri, // Your API identifier
                        scope: authConfig.scope,                   // The scope your API requires
                    },
                })
                return;
            }

            try {
                const data = await apiClient.createPortalSession(user.sub);
                if (data && data.url) {
                    window.location.replace(data.url); // Use window.location.replace to avoid adding to history
                }
                else if (data && data.sessionId) {
                    apiClient.redirectToStripeCheckout(user.sub, data.sessionId);
                }
            } catch (error) {
                
            }
        }

        createPortalSession();
    }, [user, getAccessTokenSilently, loginWithRedirect]);

    return <div>Redirecting to your customer portal...</div>;
};

export default StripePortalRedirect;

