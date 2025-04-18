import axios from 'axios';
import authConfig from '../auth_config.json';
import { loadStripe } from '@stripe/stripe-js';

// Initialize Stripe with your publishable key
const stripePromise = loadStripe(authConfig.stripePublishableKey);

const DefaultErrorMessage = "Something went wrong. If you continue to receive this error, please request support at applied.ai.help@gmail.com.";
const NullTableErrorMessage = "Please ensure you've attached SQL table schema(s) to the left.";
const TooManyRequestsMessage = "Sorry, but it looks like your usage quota has been depleted. To sign up for pay as you go billing, you can do so here: "
class ApiClient {
    constructor(baseUrl) {
        this.baseUrl = baseUrl;
        this.client = axios.create({
            baseURL: baseUrl,
        });

        // Add a request interceptor to automatically attach the access token.
        this.client.interceptors.request.use(
            async (config) => {
                try {
                    // Request a token with the proper audience and scope
                    const token = await this.getAccessTokenSilently({
                        authorizationParams: {
                            audience: authConfig.audience,
                            scope: authConfig.scope,
                        },
                    });
                    if (token) {
                        config.headers.Authorization = `Bearer ${token}`;
                    }
                } catch (error) {

                }
                return config;
            },
            (error) => Promise.reject(error)
        );

        // Bind methods
        this.redirectToStripeCheckout = this.redirectToStripeCheckout.bind(this);
    }

    async saveUserData(sub) {
        try {
            const response = await this.client.post(`promptflow/post/saveUser/${sub}`);
            if (response.status === 200) {
                return response.data;
            } else {
                return DefaultErrorMessage;
            }
        } catch (error) {
            return DefaultErrorMessage;
        }
    }

    // Helper method to retry a request
    async retryRequest(fn, retries = 6) {
        for (let attempt = 0; attempt < retries; attempt++) {
            try {
                return await fn();
            } catch (error) {
                if (error.response && [404, 401].includes(error.response.status)) {
                    return; // Do not retry 404 or 401 errors
                }

                if (attempt < retries - 1) {
                    await new Promise(resolve => setTimeout(resolve, 100 * (attempt + 1))); // Exponential backoff
                } else {
                    return;
                }
            }
        }
    }

    async createPortalSession(sub) {
        try {
            const response = await this.client.post(`/webhook/create-portal-session/${sub}`);
            if (response.status === 200) {
                if (response.data) return response.data;
            } else {
                return null;
            }
        } catch (error) {
            return null;
        }
    }

    async createCheckoutSession(sub) {
        try {
            const response = await this.client.post(`/webhook/create-checkout-session/${sub}`);
            if (response.status === 200) {
                return response.data.sessionId;
            } else {
                return null;
            }
        } catch (error) {
            return null;
        }
    }

    async redirectToStripeCheckout(sub, sessionId = null) {
        const stripe = await stripePromise;

        if (!sessionId) sessionId = await this.createCheckoutSession(sub);

        if (sessionId) {
            await stripe.redirectToCheckout({ sessionId });
        }
    }

    appendCheckoutLink(message) {
        return `${message} Please <a href="#" id="stripe-checkout-link">click here</a> to proceed to the Stripe Checkout.`;
    }

    async getNewAPIKey(username) {
        try {
            const response = await this.client.get(`/promptflow/get/newAPIKey/${username}`);
            if (response.status === 200) {
                return response.data;
            } else {
                return DefaultErrorMessage;
            }
        } catch (error) {
            return DefaultErrorMessage;
        }
    }

    async getSQLData(username) {
        try {
            const response = await this.client.get('/promptflow/get/sqlData/' + username);
            if (response.status === 200) {
                return response.data;
            } else {
                return DefaultErrorMessage;
            }
        } catch (error) {
            if (error.status === 404) return "Enter SQL table Schema(s) here to help the model with context.";
            return DefaultErrorMessage;
        }
    }

    async saveSQLData(username, sqlDefinitionsString) {
        try {
            const body = {
                Username: username,
                SqlData: sqlDefinitionsString,
            };

            const response = await this.client.post('/promptflow/post/sqlData', body);
            if (response.status === 204) {
                return response.data;
            } else {
                return DefaultErrorMessage;
            }
        } catch (error) {
            return DefaultErrorMessage;
        }
    }

    async requestSQLDataHelp(username, assistanceQuery, tableDefinitions) {
        try {
            const body = {
                Username: username,
                Query: assistanceQuery,
                SqlData: tableDefinitions
            };

            const makeRequest = async () => {
                const response = await this.client.post('/promptflow/post/sqlHelp', body);
                if (response.status === 200) {
                    return response.data;
                } else {
                    return DefaultErrorMessage;
                }
            };

            return await this.retryRequest(makeRequest);
        } catch (error) {
            if (error.response && error.response.status === 429) {
                return this.appendCheckoutLink(error.response.data || DefaultErrorMessage);
            }
            return DefaultErrorMessage;
        }
    }

    async requestSQLConversion(username, messages, tableDefinitions) {
        try {
            if (tableDefinitions === null || tableDefinitions.trim() === "") return NullTableErrorMessage;

            const body = {
                Username: username,
                Messages: messages,
                SqlData: tableDefinitions
            };

            const makeRequest = async () => {
                try {
                    const response = await this.client.post('/promptflow/post/convertQuery', body);
                    return response.data; // If 200, we get here
                } catch (error) {
                    if (error.response && error.response.status === 429) {
                        return this.appendCheckoutLink(TooManyRequestsMessage);
                    }
                    return DefaultErrorMessage;
                }
            };

            return await this.retryRequest(makeRequest);
        } catch (error) {
            return DefaultErrorMessage;
        }
    }
}

export default ApiClient;



