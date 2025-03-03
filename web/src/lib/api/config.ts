interface ApiConfig {
    baseURL: string;
}

const devConfig: ApiConfig = {
    baseURL: 'http://localhost:5031/api'
};

const prodConfig: ApiConfig = {
    baseURL: 'https://112.10.215.25:5456/api'
};

export const apiConfig: ApiConfig = import.meta.env.DEV ? devConfig : prodConfig;