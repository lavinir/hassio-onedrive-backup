import instance from './instance';

export interface BuildInfo {
  version: string;
  branch: string;
}

export const fetchBuildInfo = async (): Promise<BuildInfo> => {
  const response = await instance.get('/settings/build-info');
  return response.data;
};
