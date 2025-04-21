import React, { Component } from 'react';
import { Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import ProtectedWindowWrapper from './components/ProtectedWindowWrapper';
import ApiReference from './components/ApiReference';
import HomePage from './components/HomePage';
import StripePortalRedirect from './components/StripePortalRedirect';
import PostLoginInitializer from './components/PostLoginInitializer'; // New component
import './App.css';

export default class App extends Component {
    static displayName = App.name;

    render() {
        return (
            <Layout>
                {/* Run the initializer to save new user data on login */}
                <PostLoginInitializer />
                <Routes>
                    <Route path="/" element={<HomePage />} />
                    <Route path="/api" element={<ProtectedWindowWrapper />} />
                    <Route path="/reference" element={<ApiReference />} />
                    <Route path="/home" element={<HomePage />} />
                    <Route path="/stripe-portal" element={<StripePortalRedirect />} />
                </Routes>
            </Layout>
        );
    }
}
