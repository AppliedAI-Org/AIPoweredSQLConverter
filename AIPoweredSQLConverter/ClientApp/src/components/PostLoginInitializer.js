import { useEffect } from 'react';
import { useAuth0 } from '@auth0/auth0-react';
import ApiClient from '../api/ApiClient';
import authConfig from '../auth_config.json';

const PostLoginInitializer = () => {
    const { isAuthenticated, user, getAccessTokenSilently } = useAuth0();

    useEffect(() => {
        const registerUser = async () => {
            if (isAuthenticated && user?.sub) {
                const apiClient = new ApiClient(authConfig.ApiUri, getAccessTokenSilently);
                try {
                    // Call your backend endpoint to save the user data
                    await apiClient.saveUserData(user.sub);
                } catch (error) {
                    
                }
            }
        };

        registerUser();
    }, [isAuthenticated, user, getAccessTokenSilently]);

    return null;
};

export default PostLoginInitializer;
